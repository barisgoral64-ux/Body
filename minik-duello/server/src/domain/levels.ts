import { LEVELS, REWARDS, SCORING } from "../config/constants.js";

export type GameType =
  | "colorMatch" | "colorBox" | "findColor" | "missingColor"
  | "shapeMatch" | "findShape" | "shapeSort"
  | "countSelect"
  | "findAnimal" | "animalSound" | "animalHabitat" | "animalPair"
  | "memoryCards" | "maze" | "quickFind" | "pattern" | "puzzle" | "explorerMix";

export interface LevelConfig {
  readonly levelID: number;
  readonly worldID: number;
  readonly gameType: GameType;
  readonly difficulty: number;
  readonly roundCount: number;
  readonly targetScore: number;
  readonly objectCount: number;
  readonly colorCount: number;
  /** 0 = süre sınırı yok. Süre dolunca tur ceza olmadan biter. */
  readonly timeLimitSeconds: number;
  readonly starRequirements: readonly [number, number, number];
  readonly reward: { readonly coins: number };
  readonly unlocked: boolean;
  readonly nextLevelID: number | null;
}

interface WorldDef {
  readonly id: number;
  readonly key: string;
  readonly types: readonly GameType[];
}

export const WORLDS: readonly WorldDef[] = [
  { id: 1, key: "colors", types: ["colorMatch", "colorBox", "findColor", "missingColor"] },
  { id: 2, key: "shapes", types: ["shapeMatch", "findShape", "shapeSort"] },
  { id: 3, key: "numbers", types: ["countSelect"] },
  { id: 4, key: "animals", types: ["findAnimal", "animalSound", "animalHabitat", "animalPair"] },
  { id: 5, key: "memory", types: ["memoryCards"] },
  { id: 6, key: "maze", types: ["maze"] },
  { id: 7, key: "quickFind", types: ["quickFind"] },
  { id: 8, key: "patterns", types: ["pattern"] },
  { id: 9, key: "puzzle", types: ["puzzle"] },
  { id: 10, key: "explorer", types: ["explorerMix"] },
];

const DIFFICULTY_START = 0.05;
const DIFFICULTY_SPAN = 0.65;
/** Yeni konuya girerken zorluk hafifçe düşer: öğrenme eğrisi yumuşar. */
const WORLD_START_DIP = 0.04;
const BASE_ROUNDS = 5;
const LATE_ROUNDS_FROM_LEVEL = 41;
const LATE_ROUNDS = 6;
const TIME_LIMIT_FROM_LEVEL = 21;
const SECONDS_PER_ROUND = 25;
const WORLD_COIN_BONUS_PER_WORLD = 2;
const COLOR_COUNT_WORLD1 = [2, 2, 3, 3, 3, 4, 4, 4, 5, 5] as const;
const MEMORY_CARDS = [4, 4, 6, 6, 6, 8, 8, 10, 10, 12] as const;
const PUZZLE_PIECES = [2, 2, 3, 3, 4, 4, 4, 5, 5, 6] as const;
const NUMBER_RANGE = [3, 4, 5, 5, 6, 7, 8, 9, 10, 10] as const;
const MAZE_BASE = 4;
const DEFAULT_OBJECTS_BASE = 3;
const OBJECT_STEP_EVERY = 3;
const COLOR_COUNT_MIN = 2;
const COLOR_COUNT_MAX = 5;
const COLOR_COUNT_DIFFICULTY_SCALE = 8;
export const STAR_FRACTIONS = [0.5, 0.75, 1] as const;
export const MIN_ROUND_DURATION_MS = 1_500;

function pickFrom<T>(table: readonly T[], index: number): T {
  const v = table[Math.min(index, table.length - 1)];
  if (v === undefined) throw new Error("Boş tablo");
  return v;
}

export function difficultyFor(levelId: number): number {
  const base = DIFFICULTY_START + (DIFFICULTY_SPAN * (levelId - 1)) / (LEVELS.totalLevels - 1);
  const firstOfWorld = (levelId - 1) % LEVELS.levelsPerWorld === 0 && levelId > 1;
  return Math.round((firstOfWorld ? base - WORLD_START_DIP : base) * 1000) / 1000;
}

function objectCountFor(world: number, idx: number): number {
  switch (world) {
    case 3: return pickFrom(NUMBER_RANGE, idx);
    case 5: return pickFrom(MEMORY_CARDS, idx);
    case 6: return MAZE_BASE + Math.floor(idx / OBJECT_STEP_EVERY);
    case 9: return pickFrom(PUZZLE_PIECES, idx);
    default: return DEFAULT_OBJECTS_BASE + Math.floor(idx / OBJECT_STEP_EVERY);
  }
}

function colorCountFor(world: number, idx: number, difficulty: number): number {
  if (world === 1) return pickFrom(COLOR_COUNT_WORLD1, idx);
  const n = COLOR_COUNT_MIN + Math.floor(difficulty * COLOR_COUNT_DIFFICULTY_SCALE);
  return Math.min(COLOR_COUNT_MAX, Math.max(COLOR_COUNT_MIN, n));
}

/** 100 bölümün tamamı veriden üretilir; bölüm başına ayrı kod yoktur. */
export function buildLevelCatalog(): readonly LevelConfig[] {
  const levels: LevelConfig[] = [];
  for (let id = 1; id <= LEVELS.totalLevels; id += 1) {
    const idx = (id - 1) % LEVELS.levelsPerWorld;
    const world = WORLDS[Math.floor((id - 1) / LEVELS.levelsPerWorld)];
    if (!world) throw new Error(`Dünya bulunamadı: ${id}`);
    const difficulty = difficultyFor(id);
    const roundCount = id >= LATE_ROUNDS_FROM_LEVEL ? LATE_ROUNDS : BASE_ROUNDS;
    const targetScore = roundCount * SCORING.correctAnswer;
    levels.push({
      levelID: id,
      worldID: world.id,
      gameType: pickFrom(world.types, idx % world.types.length),
      difficulty,
      roundCount,
      targetScore,
      objectCount: objectCountFor(world.id, idx),
      colorCount: colorCountFor(world.id, idx, difficulty),
      timeLimitSeconds: id >= TIME_LIMIT_FROM_LEVEL ? roundCount * SECONDS_PER_ROUND : 0,
      starRequirements: [
        Math.round(targetScore * STAR_FRACTIONS[0]),
        Math.round(targetScore * STAR_FRACTIONS[1]),
        Math.round(targetScore * STAR_FRACTIONS[2]),
      ],
      reward: { coins: REWARDS.levelBaseCoins + world.id * WORLD_COIN_BONUS_PER_WORLD },
      unlocked: id === 1,
      nextLevelID: id < LEVELS.totalLevels ? id + 1 : null,
    });
  }
  return levels;
}

export function starsForScore(level: LevelConfig, score: number): number {
  let stars = 0;
  for (const threshold of level.starRequirements) if (score >= threshold) stars += 1;
  return stars;
}
