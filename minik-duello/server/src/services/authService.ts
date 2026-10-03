import { createHmac } from "node:crypto";
import { AUTH } from "../config/constants.js";
import { generateFriendCode, generateUsername } from "../domain/identity.js";
import { defaultParentSettings, type Player, type PlayerProfile } from "../domain/models.js";
import { STARTER_REWARDS } from "../config/constants.js";
import type { Clock, IdGenerator } from "../infra/runtime.js";
import type { DataStore } from "../infra/store.js";
import type { TokenService } from "../security/tokens.js";
import { ErrorCode, fail, ok, type Result } from "../shared/result.js";

export interface AuthTokens {
  readonly accessToken: string;
  readonly refreshToken: string;
}

const MAX_CODE_ATTEMPTS = 50;

export class AuthService {
  constructor(
    private readonly store: DataStore,
    private readonly tokens: TokenService,
    private readonly clock: Clock,
    private readonly newId: IdGenerator,
    private readonly deviceSecret: string,
  ) {}

  /** Anonim hesap: e-posta/telefon/isim alınmaz. Aynı cihaz aynı hesaba döner. */
  async registerAnonymous(deviceId: string): Promise<Result<{ player: Player; tokens: AuthTokens; isNew: boolean }>> {
    if (deviceId.length < AUTH.deviceIdMinLength || deviceId.length > AUTH.deviceIdMaxLength) {
      return fail(ErrorCode.InvalidInput, "Geçersiz cihaz kimliği");
    }
    const hash = createHmac("sha256", this.deviceSecret).update(deviceId).digest("hex");

    const existing = await this.store.getPlayerByDeviceHash(hash);
    if (existing) {
      await this.store.touchPlayer(existing.playerId, this.clock.now());
      return ok({ player: existing, tokens: this.issue(existing.playerId), isNew: false });
    }

    const friendCode = await this.uniqueFriendCode();
    if (!friendCode) return fail(ErrorCode.Internal, "Arkadaş kodu üretilemedi");

    const now = this.clock.now();
    const player: Player = {
      playerId: this.newId(),
      friendCode,
      username: generateUsername(),
      createdAt: now,
      lastSeenAt: now,
    };
    const profile: PlayerProfile = {
      playerId: player.playerId,
      avatarCharacter: "panda",
      avatarFrame: null,
      level: 1,
      totalStars: 0,
      coins: 0,
    };
    await this.store.createPlayer(player, hash, profile, {
      ...defaultParentSettings(player.playerId),
      pinHash: null,
      pinFailedAttempts: 0,
      pinLockedUntil: null,
    });
    for (const rewardId of STARTER_REWARDS) await this.store.grantInventory(player.playerId, rewardId, now);
    return ok({ player, tokens: this.issue(player.playerId), isNew: true });
  }

  async refresh(refreshToken: string): Promise<Result<AuthTokens>> {
    const verified = this.tokens.verify(refreshToken, "refresh");
    if (!verified.ok) return verified;
    const player = await this.store.getPlayer(verified.value);
    if (!player) return fail(ErrorCode.Unauthorized, "Oyuncu bulunamadı");
    return ok(this.issue(player.playerId));
  }

  verifyAccessToken(accessToken: string): Result<string> {
    return this.tokens.verify(accessToken, "access");
  }

  private issue(playerId: string): AuthTokens {
    return {
      accessToken: this.tokens.sign(playerId, "access", AUTH.accessTokenTtlSeconds),
      refreshToken: this.tokens.sign(playerId, "refresh", AUTH.refreshTokenTtlSeconds),
    };
  }

  private async uniqueFriendCode(): Promise<string | null> {
    for (let i = 0; i < MAX_CODE_ATTEMPTS; i += 1) {
      const code = generateFriendCode();
      if (!(await this.store.getPlayerByFriendCode(code))) return code;
    }
    return null;
  }
}
