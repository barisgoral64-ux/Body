import { SCORING } from "../config/constants.js";

export interface AnswerInput {
  readonly correct: boolean;
  /** Sunucunun ölçtüğü cevap süresi (istemci saatine güvenilmez). */
  readonly responseMs: number;
  readonly roundDurationMs: number;
  readonly currentStreak: number;
}

export interface AnswerScore {
  readonly points: number;
  readonly newStreak: number;
}

/** Hız bonusu: round süresine göre doğrusal, [speedBonusMin, speedBonusMax]. İnsan altı hız bonus almaz. */
export function speedBonus(responseMs: number, roundDurationMs: number): number {
  if (responseMs < SCORING.minHumanResponseMs || roundDurationMs <= 0) return 0;
  const remainingRatio = Math.min(1, Math.max(0, 1 - responseMs / roundDurationMs));
  const range = SCORING.speedBonusMax - SCORING.speedBonusMin;
  return Math.round(SCORING.speedBonusMin + range * remainingRatio);
}

/** Puan asla negatif olmaz; yanlış cevap seriyi sıfırlar ama puan düşürmez. */
export function scoreAnswer(input: AnswerInput): AnswerScore {
  if (!input.correct) return { points: SCORING.wrongAnswer, newStreak: 0 };
  const bonus = speedBonus(input.responseMs, input.roundDurationMs);
  const streakBonus = input.currentStreak > 0 ? SCORING.streakBonus : 0;
  return { points: SCORING.correctAnswer + bonus + streakBonus, newStreak: input.currentStreak + 1 };
}
