import { randomBytes, scryptSync, timingSafeEqual } from "node:crypto";
import { AUTH, PARENT } from "../config/constants.js";
import type { ParentSettings, PlayerId } from "../domain/models.js";
import type { Clock } from "../infra/runtime.js";
import type { DataStore, StoredParentSettings } from "../infra/store.js";
import { ErrorCode, fail, ok, type Result } from "../shared/result.js";

const PIN_PATTERN = new RegExp(`^\\d{${PARENT.pinLength}}$`);
const MAX_DAILY_LIMIT_MINUTES = 24 * 60;
const SOCIAL_FLAGS = ["friendsEnabled", "multiplayerEnabled", "onlineStatusVisible", "gameInvitationsEnabled"] as const;

export type SettingsPatch = Partial<Omit<ParentSettings, "playerId">>;
export type PublicParentSettings = ParentSettings & { readonly hasPin: boolean };

export function hashPin(pin: string): string {
  const salt = randomBytes(AUTH.saltBytes);
  const hash = scryptSync(pin, salt, AUTH.scryptKeyLength);
  return `${salt.toString("hex")}:${hash.toString("hex")}`;
}

function pinMatches(pin: string, stored: string): boolean {
  const [saltHex, hashHex] = stored.split(":");
  if (!saltHex || !hashHex) return false;
  const expected = Buffer.from(hashHex, "hex");
  const actual = scryptSync(pin, Buffer.from(saltHex, "hex"), expected.length);
  return timingSafeEqual(actual, expected);
}

export class ParentControlService {
  constructor(
    private readonly store: DataStore,
    private readonly clock: Clock,
    /** Engelleme/kaldırma sonrası canlı oda ve davetleri kapatmak için. */
    private readonly onRelationshipRemoved: (a: PlayerId, b: PlayerId) => void = () => undefined,
    /** Hesap silinmeden önce canlı oda ve bağlantıyı kapatmak için. */
    private readonly onAccountDeleting: (playerId: PlayerId) => void = () => undefined,
  ) {}

  async getSettings(playerId: PlayerId): Promise<Result<PublicParentSettings>> {
    const s = await this.store.getSettings(playerId);
    if (!s) return fail(ErrorCode.NotFound, "Ayar bulunamadı");
    return ok(toPublic(s));
  }

  async setPin(playerId: PlayerId, newPin: string, currentPin?: string): Promise<Result<void>> {
    if (!PIN_PATTERN.test(newPin)) return fail(ErrorCode.InvalidInput, "PIN geçersiz");
    const s = await this.store.getSettings(playerId);
    if (!s) return fail(ErrorCode.NotFound, "Ayar bulunamadı");
    if (s.pinHash) {
      const check = await this.verifyPin(playerId, currentPin ?? "");
      if (!check.ok) return check;
    }
    const fresh = (await this.store.getSettings(playerId)) ?? s;
    await this.store.saveSettings({ ...fresh, pinHash: hashPin(newPin), pinFailedAttempts: 0, pinLockedUntil: null, pinLockoutCount: 0 });
    return ok(undefined);
  }

  /** Doğru PIN sayacı sıfırlar; yanlış PIN art arda denemede kilitler. */
  async verifyPin(playerId: PlayerId, pin: string): Promise<Result<void>> {
    const s = await this.store.getSettings(playerId);
    if (!s) return fail(ErrorCode.NotFound, "Ayar bulunamadı");
    if (!s.pinHash) return fail(ErrorCode.Forbidden, "PIN ayarlanmamış");
    const now = this.clock.now();
    if (s.pinLockedUntil && s.pinLockedUntil > now) return fail(ErrorCode.RateLimited, "PIN geçici olarak kilitli");

    if (PIN_PATTERN.test(pin) && pinMatches(pin, s.pinHash)) {
      if (s.pinFailedAttempts > 0 || s.pinLockedUntil || s.pinLockoutCount > 0) {
        await this.store.saveSettings({ ...s, pinFailedAttempts: 0, pinLockedUntil: null, pinLockoutCount: 0 });
      }
      return ok(undefined);
    }

    const attempts = s.pinFailedAttempts + 1;
    const locked = attempts >= PARENT.maxPinAttempts;
    // Her ardışık kilitlenmede süre büyür (5 dk, 15 dk, 45 dk … en çok 24 saat).
    const duration = Math.min(PARENT.pinLockoutMaxMs, PARENT.pinLockoutMs * PARENT.pinLockoutGrowth ** s.pinLockoutCount);
    await this.store.saveSettings({
      ...s,
      pinFailedAttempts: locked ? 0 : attempts,
      pinLockedUntil: locked ? new Date(now.getTime() + duration) : s.pinLockedUntil,
      pinLockoutCount: locked ? s.pinLockoutCount + 1 : s.pinLockoutCount,
    });
    return fail(locked ? ErrorCode.RateLimited : ErrorCode.Forbidden, "PIN yanlış");
  }

  async updateSettings(playerId: PlayerId, patch: SettingsPatch, pin?: string): Promise<Result<PublicParentSettings>> {
    const current = await this.store.getSettings(playerId);
    if (!current) return fail(ErrorCode.NotFound, "Ayar bulunamadı");

    if (patch.dailyLimitMinutes !== undefined) {
      const v = patch.dailyLimitMinutes;
      if (!Number.isInteger(v) || v < 0 || v > MAX_DAILY_LIMIT_MINUTES) return fail(ErrorCode.InvalidInput, "Süre sınırı geçersiz");
    }

    const enablesSocial = SOCIAL_FLAGS.some((f) => patch[f] === true && !current[f]);
    if (current.pinHash) {
      const check = await this.verifyPin(playerId, pin ?? "");
      if (!check.ok) return check;
    } else if (enablesSocial) {
      // PIN yokken sosyal özellik açılamaz: ebeveyn önce PIN belirlemelidir.
      return fail(ErrorCode.Forbidden, "Önce ebeveyn PIN'i belirlenmeli");
    }

    const fresh = (await this.store.getSettings(playerId)) ?? current;
    const next: StoredParentSettings = {
      ...fresh,
      friendsEnabled: patch.friendsEnabled ?? fresh.friendsEnabled,
      multiplayerEnabled: patch.multiplayerEnabled ?? fresh.multiplayerEnabled,
      onlineStatusVisible: patch.onlineStatusVisible ?? fresh.onlineStatusVisible,
      gameInvitationsEnabled: patch.gameInvitationsEnabled ?? fresh.gameInvitationsEnabled,
      dailyLimitMinutes: patch.dailyLimitMinutes ?? fresh.dailyLimitMinutes,
      soundEnabled: patch.soundEnabled ?? fresh.soundEnabled,
    };
    await this.store.saveSettings(next);
    return ok(toPublic(next));
  }

  async blockPlayer(playerId: PlayerId, blockedId: PlayerId, pin: string): Promise<Result<void>> {
    if (playerId === blockedId) return fail(ErrorCode.InvalidInput, "Kendini engelleyemezsin");
    const check = await this.verifyPin(playerId, pin);
    if (!check.ok) return check;
    if (!(await this.store.getPlayer(blockedId))) return fail(ErrorCode.NotFound, "Oyuncu bulunamadı");
    const now = this.clock.now();
    await this.store.addBlock(playerId, blockedId, now);
    await this.store.removeFriendPair(playerId, blockedId);
    await this.store.cancelPendingBetween(playerId, blockedId, now);
    this.onRelationshipRemoved(playerId, blockedId);
    return ok(undefined);
  }

  async unblockPlayer(playerId: PlayerId, blockedId: PlayerId, pin: string): Promise<Result<void>> {
    const check = await this.verifyPin(playerId, pin);
    if (!check.ok) return check;
    await this.store.removeBlock(playerId, blockedId);
    return ok(undefined);
  }

  async removeFriend(playerId: PlayerId, friendId: PlayerId, pin: string): Promise<Result<void>> {
    const check = await this.verifyPin(playerId, pin);
    if (!check.ok) return check;
    await this.store.removeFriendPair(playerId, friendId);
    this.onRelationshipRemoved(playerId, friendId);
    return ok(undefined);
  }

  /**
   * Hesabı ve tüm verisini kalıcı olarak siler (ebeveyn PIN'i ile). Geri alınamaz.
   * Çocuk verisi silme hakkı: COPPA / GDPR / KVKK taleplerinin teknik karşılığı.
   */
  async deleteAccount(playerId: PlayerId, pin: string): Promise<Result<void>> {
    const check = await this.verifyPin(playerId, pin);
    if (!check.ok) return check;
    this.onAccountDeleting(playerId);
    await this.store.deletePlayer(playerId);
    return ok(undefined);
  }

  listBlocked(playerId: PlayerId): Promise<readonly PlayerId[]> {
    return this.store.listBlocked(playerId);
  }

  isBlocked(a: PlayerId, b: PlayerId): Promise<boolean> {
    return this.store.isBlockedEitherWay(a, b);
  }
}

function toPublic(s: StoredParentSettings): PublicParentSettings {
  return {
    playerId: s.playerId,
    friendsEnabled: s.friendsEnabled,
    multiplayerEnabled: s.multiplayerEnabled,
    onlineStatusVisible: s.onlineStatusVisible,
    gameInvitationsEnabled: s.gameInvitationsEnabled,
    dailyLimitMinutes: s.dailyLimitMinutes,
    soundEnabled: s.soundEnabled,
    hasPin: s.pinHash !== null,
  };
}
