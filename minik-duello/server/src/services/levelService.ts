import { REWARDS } from "../config/constants.js";
import { buildLevelCatalog, MIN_ROUND_DURATION_MS, starsForScore, type LevelConfig } from "../domain/levels.js";
import type { PlayerId } from "../domain/models.js";
import type { Clock } from "../infra/runtime.js";
import type { DataStore } from "../infra/store.js";
import { ErrorCode, fail, ok, type Result } from "../shared/result.js";
import type { RewardService } from "./rewardService.js";

const MAX_DURATION_MS = 24 * 60 * 60 * 1000;

export interface LevelResultView {
  readonly stars: number;
  readonly starsGained: number;
  readonly coinsGained: number;
  readonly newRewards: readonly string[];
  readonly nextLevelId: number | null;
}

export class LevelService {
  private readonly catalog: readonly LevelConfig[] = buildLevelCatalog();

  constructor(
    private readonly store: DataStore,
    private readonly rewards: RewardService,
    private readonly clock: Clock,
  ) {}

  getCatalog(): readonly LevelConfig[] {
    return this.catalog;
  }

  async getProgress(playerId: PlayerId): Promise<{ levels: readonly { levelId: number; stars: number; bestScore: number; unlocked: boolean }[] }> {
    const progress = new Map((await this.store.listProgress(playerId)).map((p) => [p.levelId, p]));
    return {
      levels: this.catalog.map((l) => ({
        levelId: l.levelID,
        stars: progress.get(l.levelID)?.stars ?? 0,
        bestScore: progress.get(l.levelID)?.bestScore ?? 0,
        unlocked: l.levelID === 1 || (progress.get(l.levelID - 1)?.stars ?? 0) >= 1,
      })),
    };
  }

  /**
   * İstemci yalnızca ham skoru ve süreyi bildirir; yıldız, coin ve kilit açma sunucuda karar verilir.
   * Makul olmayan skor/süre (örn. insan altı hız) reddedilir.
   */
  async submitResult(playerId: PlayerId, levelId: number, score: number, durationMs: number): Promise<Result<LevelResultView>> {
    const level = this.catalog.find((l) => l.levelID === levelId);
    if (!level) return fail(ErrorCode.NotFound, "Bölüm yok");
    if (!Number.isInteger(score) || score < 0 || score > level.targetScore) return fail(ErrorCode.InvalidInput, "Skor geçersiz");
    if (!Number.isFinite(durationMs) || durationMs < level.roundCount * MIN_ROUND_DURATION_MS || durationMs > MAX_DURATION_MS) {
      return fail(ErrorCode.InvalidInput, "Süre geçersiz");
    }

    if (levelId > 1) {
      const previous = await this.store.getProgress(playerId, levelId - 1);
      if ((previous?.stars ?? 0) < 1) return fail(ErrorCode.Forbidden, "Bölüm kilitli");
    }

    const stars = Math.min(REWARDS.maxStarsPerLevel, starsForScore(level, score));
    const existing = await this.store.getProgress(playerId, levelId);
    const previousStars = existing?.stars ?? 0;
    const starsGained = Math.max(0, stars - previousStars);
    const firstClear = previousStars === 0 && stars >= 1;

    await this.store.upsertProgress({
      playerId,
      levelId,
      stars: Math.max(stars, previousStars),
      bestScore: Math.max(score, existing?.bestScore ?? 0),
      completedAt: stars >= 1 ? (existing?.completedAt ?? this.clock.now()) : (existing?.completedAt ?? null),
    });

    // Tekrar oynayarak coin çiftçiliği yok: yalnızca yeni yıldız veya ilk geçiş ödül verir.
    const coinsGained = (firstClear ? level.reward.coins : 0) + starsGained * REWARDS.levelCoinsPerStar;
    const summary = await this.rewards.grant(playerId, starsGained, coinsGained, "level", String(levelId));
    return ok({ stars, starsGained, coinsGained, newRewards: summary.newRewards, nextLevelId: stars >= 1 ? level.nextLevelID : null });
  }
}
