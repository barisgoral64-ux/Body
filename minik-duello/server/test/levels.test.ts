import { describe, expect, it } from "vitest";
import { LEVELS } from "../src/config/constants.js";
import { buildLevelCatalog, starsForScore } from "../src/domain/levels.js";

const catalog = buildLevelCatalog();
const level = (id: number) => {
  const l = catalog[id - 1];
  if (!l) throw new Error("yok");
  return l;
};

describe("bölüm kataloğu", () => {
  it("100 bölüm, 10 dünya, ardışık kimlik ve zincir", () => {
    expect(catalog).toHaveLength(LEVELS.totalLevels);
    catalog.forEach((l, i) => {
      expect(l.levelID).toBe(i + 1);
      expect(l.worldID).toBe(Math.floor(i / LEVELS.levelsPerWorld) + 1);
      expect(l.nextLevelID).toBe(i === catalog.length - 1 ? null : i + 2);
    });
  });
  it("yalnızca ilk bölüm açık başlar", () => {
    expect(catalog.filter((l) => l.unlocked).map((l) => l.levelID)).toEqual([1]);
  });
  it("zorluk hiçbir zaman ani artmaz", () => {
    for (let i = 1; i < catalog.length; i += 1) {
      const step = level(i + 1).difficulty - level(i).difficulty;
      expect(step).toBeLessThanOrEqual(LEVELS.maxDifficultyStep);
    }
  });
  it("zorluk bantları istenen sırada", () => {
    expect(level(5).difficulty).toBeLessThan(0.1);
    expect(level(10).difficulty).toBeLessThan(0.15);
    expect(level(20).difficulty).toBeLessThan(0.25);
    expect(level(40).difficulty).toBeLessThan(0.4);
    expect(level(100).difficulty).toBeGreaterThan(level(40).difficulty);
  });
  it("yeni dünyanın ilk bölümünde zorluk hafifçe düşer", () => {
    expect(level(11).difficulty).toBeLessThan(level(10).difficulty);
    expect(level(21).difficulty).toBeLessThan(level(20).difficulty);
  });
  it("dünya 1: 2 renkle başlar, 5 renge çıkar", () => {
    expect(level(1).colorCount).toBe(2);
    expect(level(10).colorCount).toBe(5);
    expect(level(1).gameType).toBe("colorMatch");
  });
  it("hafıza dünyası 4 kartla başlar ve artar", () => {
    expect(level(41).objectCount).toBe(4);
    expect(level(50).objectCount).toBeGreaterThanOrEqual(10);
  });
  it("puzzle 2-6 parça, sayı dünyası 1-10 aralığında", () => {
    expect(level(81).objectCount).toBe(2);
    expect(level(90).objectCount).toBe(6);
    expect(Math.max(...catalog.filter((l) => l.worldID === 3).map((l) => l.objectCount))).toBe(10);
  });
  it("yıldız eşikleri artan, süre sınırı erken bölümlerde yok", () => {
    for (const l of catalog) {
      const [a, b, c] = l.starRequirements;
      expect(a).toBeLessThan(b);
      expect(b).toBeLessThanOrEqual(c);
      expect(c).toBe(l.targetScore);
    }
    expect(level(1).timeLimitSeconds).toBe(0);
    expect(level(30).timeLimitSeconds).toBeGreaterThan(0);
  });
  it("skordan yıldız hesabı", () => {
    const l = level(1);
    expect(starsForScore(l, 0)).toBe(0);
    expect(starsForScore(l, l.starRequirements[0])).toBe(1);
    expect(starsForScore(l, l.targetScore)).toBe(3);
  });
});
