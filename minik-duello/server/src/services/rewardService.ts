import { DAILY_REWARD_CYCLE, STAR_UNLOCKS } from "../config/constants.js";
import { REWARD_CATALOG, rewardById, CHARACTER_REWARD_PREFIX } from "../domain/rewards.js";
import type { CharacterId, PlayerId } from "../domain/models.js";
import { CHARACTER_IDS } from "../domain/models.js";
import type { Clock } from "../infra/runtime.js";
import type { DataStore } from "../infra/store.js";
import { ErrorCode, fail, ok, type Result } from "../shared/result.js";
import { levelForStars } from "./playerService.js";

export interface GrantSummary {
  readonly totalStars: number;
  readonly coins: number;
  readonly newRewards: readonly string[];
}

const MS_PER_DAY = 24 * 60 * 60 * 1000;
const dayKey = (d: Date): string => d.toISOString().slice(0, 10);

export interface DailyStatus {
  readonly canClaim: boolean;
  readonly nextDayIndex: number;
  readonly cycleLength: number;
}

export class RewardService {
  constructor(
    private readonly store: DataStore,
    private readonly clock: Clock,
  ) {}

  /** Yıldız ekler, seviyeyi günceller, eşiği geçen kozmetikleri açar. */
  async grant(playerId: PlayerId, stars: number, coins: number, reason: string, refId: string | null): Promise<GrantSummary> {
    const now = this.clock.now();
    let totalStars = (await this.store.getProfile(playerId))?.totalStars ?? 0;
    if (stars > 0) totalStars = await this.store.addStars(playerId, stars, now);
    let balance = (await this.store.getProfile(playerId))?.coins ?? 0;
    if (coins > 0) balance = await this.store.addCoins(playerId, coins, reason, refId);

    await this.store.updateProfile(playerId, { level: levelForStars(totalStars) });

    const newRewards: string[] = [];
    for (const unlock of STAR_UNLOCKS) {
      if (totalStars >= unlock.stars && (await this.store.grantInventory(playerId, unlock.rewardId, now))) {
        newRewards.push(unlock.rewardId);
      }
    }
    return { totalStars, coins: balance, newRewards };
  }

  async getDailyStatus(playerId: PlayerId): Promise<DailyStatus> {
    const state = await this.store.getDailyClaim(playerId);
    const today = dayKey(this.clock.now());
    return {
      canClaim: state?.lastClaimDay !== today,
      nextDayIndex: (state?.claimCount ?? 0) % DAILY_REWARD_CYCLE.length,
      cycleLength: DAILY_REWARD_CYCLE.length,
    };
  }

  /** Günlük ödül: günde bir kez. Kaçırılan gün serisi bozmaz; takvim kaldığı yerden devam eder. */
  claimDaily(playerId: PlayerId): Promise<Result<{ coins: number; rewardId: string | null; dayIndex: number }>> {
    return this.store.withPlayerLock(playerId, () => this.claimDailyLocked(playerId));
  }

  private async claimDailyLocked(playerId: PlayerId): Promise<Result<{ coins: number; rewardId: string | null; dayIndex: number }>> {
    const status = await this.getDailyStatus(playerId);
    if (!status.canClaim) return fail(ErrorCode.InvalidState, "Bugünün ödülü alındı");

    const entry = DAILY_REWARD_CYCLE[status.nextDayIndex];
    if (!entry) return fail(ErrorCode.Internal, "Ödül tanımı yok");
    const now = this.clock.now();
    const prev = await this.store.getDailyClaim(playerId);

    if (entry.coins > 0) await this.store.addCoins(playerId, entry.coins, "daily", dayKey(now));
    if (entry.rewardId) await this.store.grantInventory(playerId, entry.rewardId, now);
    await this.store.putDailyClaim(playerId, { claimCount: (prev?.claimCount ?? 0) + 1, lastClaimDay: dayKey(now) });
    return ok({ coins: entry.coins, rewardId: entry.rewardId, dayIndex: status.nextDayIndex });
  }

  async listInventory(playerId: PlayerId): Promise<readonly { rewardId: string; equipped: boolean }[]> {
    return this.store.listInventory(playerId);
  }

  /** Coin ile satın alma yalnızca oyun içi kazanılmış coin kullanır. */
  buy(playerId: PlayerId, rewardId: string): Promise<Result<{ coins: number }>> {
    return this.store.withPlayerLock(playerId, () => this.buyLocked(playerId, rewardId));
  }

  private async buyLocked(playerId: PlayerId, rewardId: string): Promise<Result<{ coins: number }>> {
    const def = rewardById(rewardId);
    if (!def || def.shopPrice === null) return fail(ErrorCode.NotFound, "Mağazada yok");
    const profile = await this.store.getProfile(playerId);
    if (!profile) return fail(ErrorCode.NotFound, "Oyuncu yok");
    if (profile.coins < def.shopPrice) return fail(ErrorCode.InvalidState, "Yeterli coin yok");
    const owned = (await this.store.listInventory(playerId)).some((i) => i.rewardId === rewardId);
    if (owned) return fail(ErrorCode.InvalidState, "Zaten sahipsin");
    const coins = await this.store.spendCoins(playerId, def.shopPrice, "shop", rewardId);
    if (coins === null) return fail(ErrorCode.InvalidState, "Yeterli coin yok");
    await this.store.grantInventory(playerId, rewardId, this.clock.now());
    return ok({ coins });
  }

  async equip(playerId: PlayerId, rewardId: string, equipped: boolean): Promise<Result<void>> {
    const def = rewardById(rewardId);
    const inventory = await this.store.listInventory(playerId);
    if (!def || !inventory.some((i) => i.rewardId === rewardId)) return fail(ErrorCode.NotFound, "Öğe sende yok");

    if (def.type === "character") {
      const id = rewardId.slice(CHARACTER_REWARD_PREFIX.length) as CharacterId;
      if (!CHARACTER_IDS.includes(id)) return fail(ErrorCode.InvalidInput, "Geçersiz karakter");
      await this.store.updateProfile(playerId, { avatarCharacter: id });
      return ok(undefined);
    }

    if (equipped) {
      for (const item of inventory) {
        if (rewardById(item.rewardId)?.slot === def.slot && item.equipped) await this.store.setEquipped(playerId, item.rewardId, false);
      }
    }
    await this.store.setEquipped(playerId, rewardId, equipped);
    if (def.type === "frame") await this.store.updateProfile(playerId, { avatarFrame: equipped ? rewardId : null });
    return ok(undefined);
  }
}

export const REWARD_IDS: readonly string[] = REWARD_CATALOG.map((r) => r.id);
export const DAY_MS = MS_PER_DAY;
