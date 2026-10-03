import { ROUND_DURATIONS_MS, ROUND_TIMING } from "../config/constants.js";
import type { RandomInt } from "../domain/identity.js";
import { COOP_MODES, type GameMode } from "../domain/models.js";
import type { RoundChoice, RoundKind, RoundView } from "../protocol.js";

export const COLORS = ["red", "blue", "green", "yellow", "orange", "purple"] as const;
export const SHAPES = ["circle", "square", "triangle", "rectangle", "star", "heart"] as const;
export const SYMBOLS = ["apple", "ball", "car", "fish", "sun", "moon"] as const;
export const PUZZLE_IMAGES = ["cat", "house", "tree", "boat"] as const;
const DECOY_SHAPES = ["circle", "square", "triangle", "heart"] as const;
const NUMBER_MIN = 1;
const NUMBER_MAX = 10;
const MEMORY_SEQUENCE_LENGTH = 3;

export type BuiltView = Omit<RoundView, "roundIndex" | "totalRounds">;
export interface BuiltRound {
  readonly view: BuiltView;
  readonly correct: ReadonlySet<string>;
}

function shuffle<T>(items: readonly T[], random: RandomInt): T[] {
  const a = [...items];
  for (let i = a.length - 1; i > 0; i -= 1) {
    const j = random(i + 1);
    const tmp = a[i] as T;
    a[i] = a[j] as T;
    a[j] = tmp;
  }
  return a;
}

const sample = <T>(items: readonly T[], count: number, random: RandomInt): T[] => shuffle(items, random).slice(0, count);

function choicesWith(correct: string, others: readonly string[], random: RandomInt): RoundChoice[] {
  const distractors = sample(others.filter((o) => o !== correct), ROUND_TIMING.choicesPerRound - 1, random);
  return shuffle([correct, ...distractors], random).map((id) => ({ id, glyph: id }));
}

export function buildRound(kind: RoundKind, random: RandomInt, newId: () => string): BuiltRound {
  const base = { roundId: newId(), kind, multi: false, showMs: 0, durationMs: ROUND_DURATIONS_MS[kind] };

  switch (kind) {
    case "color": {
      const target = sample(COLORS, 1, random)[0] as string;
      return { view: { ...base, promptKey: "findColor", params: { target }, choices: choicesWith(target, COLORS, random) }, correct: new Set([target]) };
    }
    case "shape": {
      const target = sample(SHAPES, 1, random)[0] as string;
      return { view: { ...base, promptKey: "findShape", params: { target }, choices: choicesWith(target, SHAPES, random) }, correct: new Set([target]) };
    }
    case "number": {
      const n = NUMBER_MIN + random(NUMBER_MAX - NUMBER_MIN + 1);
      const all = Array.from({ length: NUMBER_MAX - NUMBER_MIN + 1 }, (_, i) => String(NUMBER_MIN + i));
      return { view: { ...base, promptKey: "countSelect", params: { count: n }, choices: choicesWith(String(n), all, random) }, correct: new Set([String(n)]) };
    }
    case "memory": {
      const sequence = sample(SYMBOLS, MEMORY_SEQUENCE_LENGTH, random);
      const position = random(MEMORY_SEQUENCE_LENGTH);
      const target = sequence[position] as string;
      return {
        view: {
          ...base,
          showMs: ROUND_TIMING.memoryShowMs,
          promptKey: "memoryPosition",
          params: { sequence: sequence.join(","), position: position + 1 },
          choices: choicesWith(target, SYMBOLS, random),
        },
        correct: new Set([target]),
      };
    }
    case "puzzle": {
      const [image, ...others] = sample(PUZZLE_IMAGES, PUZZLE_IMAGES.length, random) as [string, ...string[]];
      const missing = random(ROUND_TIMING.puzzlePieces);
      const correct = `${image}_${missing}`;
      const decoys = others.map((o) => `${o}_${random(ROUND_TIMING.puzzlePieces)}`);
      const choices = shuffle([correct, ...decoys.slice(0, ROUND_TIMING.choicesPerRound - 1)], random).map((id) => ({ id, glyph: id }));
      return { view: { ...base, promptKey: "puzzleMissing", params: { image, pieces: ROUND_TIMING.puzzlePieces, missing: missing + 1 }, choices }, correct: new Set([correct]) };
    }
    case "stars": {
      const slots = Array.from({ length: ROUND_TIMING.starFieldSize }, (_, i) => i);
      const starSlots = new Set(sample(slots, ROUND_TIMING.starsPerRound, random));
      const choices: RoundChoice[] = slots.map((i) => ({
        id: `o${i + 1}`,
        glyph: starSlots.has(i) ? "star" : (DECOY_SHAPES[random(DECOY_SHAPES.length)] as string),
      }));
      const correct = new Set(choices.filter((c) => c.glyph === "star").map((c) => c.id));
      return { view: { ...base, multi: true, promptKey: "findStars", params: { target: ROUND_TIMING.starsPerRound }, choices }, correct };
    }
  }
}

const repeat = (kind: RoundKind, n: number): RoundKind[] => Array.from({ length: n }, () => kind);

export function planRounds(mode: GameMode): readonly RoundKind[] {
  switch (mode) {
    case "colorRace": return repeat("color", 5);
    case "shapeRace": return repeat("shape", 5);
    case "numberRace": return repeat("number", 5);
    case "memoryDuel": return repeat("memory", 5);
    case "puzzleRace": return repeat("puzzle", 5);
    case "starCollect": return repeat("stars", ROUND_TIMING.starCollectRounds);
    case "coopStars": return repeat("stars", ROUND_TIMING.coopStarsMaxRounds);
    case "coopPuzzle": return repeat("puzzle", ROUND_TIMING.coopPuzzleRounds);
    case "mixedMatch": return ["color", "shape", "number", "memory", "puzzle"];
  }
}

export const isCoopMode = (mode: GameMode): boolean => COOP_MODES.includes(mode);
