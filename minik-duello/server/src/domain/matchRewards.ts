import { REWARDS } from "../config/constants.js";
import { COOP_MODES, type GameMode, type MatchEndReason, type PlayerId } from "./models.js";

export interface PlayerOutcome {
  readonly playerId: PlayerId;
  readonly score: number;
  readonly isWinner: boolean;
  readonly starsAwarded: number;
  readonly coinsAwarded: number;
}

export interface OutcomeInput {
  readonly mode: GameMode;
  readonly reason: MatchEndReason;
  readonly scores: Readonly<Record<PlayerId, number>>;
  readonly players: readonly [PlayerId, PlayerId];
  /** En az bir tur başladıysa true; hiç oynanmadan biten odalar ödül üretmez (istismar önleme). */
  readonly roundsStarted: number;
  readonly coopSuccess: boolean;
}

/**
 * Ödülleri sunucu hesaplar. Kural: kimse ödülsüz bırakılmaz (en az asgari ödül),
 * bağlantı kopması kimseyi cezalandırmaz. Beraberlikte iki oyuncu da kazanan sayılır.
 */
export function computeOutcomes(input: OutcomeInput): readonly PlayerOutcome[] {
  const [p1, p2] = input.players;
  const score = (id: PlayerId): number => Math.max(0, input.scores[id] ?? 0);

  const base = (id: PlayerId): Omit<PlayerOutcome, "isWinner" | "starsAwarded" | "coinsAwarded"> => ({
    playerId: id,
    score: score(id),
  });

  if (input.roundsStarted === 0) {
    return [p1, p2].map((id) => ({ ...base(id), isWinner: false, starsAwarded: 0, coinsAwarded: 0 }));
  }

  if (input.reason !== "completed") {
    return [p1, p2].map((id) => ({
      ...base(id),
      isWinner: false,
      starsAwarded: REWARDS.matchMinimumStars,
      coinsAwarded: REWARDS.matchMinimumCoins,
    }));
  }

  if (COOP_MODES.includes(input.mode)) {
    const stars = input.coopSuccess ? REWARDS.coopSuccessStars : REWARDS.coopTryStars;
    const coins = input.coopSuccess ? REWARDS.coopSuccessCoins : REWARDS.coopTryCoins;
    return [p1, p2].map((id) => ({ ...base(id), isWinner: input.coopSuccess, starsAwarded: stars, coinsAwarded: coins }));
  }

  const top = Math.max(score(p1), score(p2));
  return [p1, p2].map((id) => {
    const winner = score(id) === top;
    return {
      ...base(id),
      isWinner: winner,
      starsAwarded: winner ? REWARDS.matchWinnerStars : REWARDS.matchParticipantStars,
      coinsAwarded: winner ? REWARDS.matchWinnerCoins : REWARDS.matchParticipantCoins,
    };
  });
}
