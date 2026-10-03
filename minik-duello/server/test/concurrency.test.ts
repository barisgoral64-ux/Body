import { describe, expect, it } from "vitest";
import { createEnv, newPlayer } from "./helpers.js";

const GOOD_DURATION = 20_000;

describe("eşzamanlı çift istek (yarış durumu) koruması", () => {
  it("aynı bölüm sonucu paralel gönderilince ödül yalnızca bir kez verilir", async () => {
    const env = await createEnv();
    const p = await newPlayer(env);
    const target = env.c.levels.getCatalog()[0]?.targetScore ?? 0;
    const results = await Promise.all(Array.from({ length: 8 }, () => env.c.levels.submitResult(p.id, 1, target, GOOD_DURATION)));
    expect(results.every((r) => r.ok)).toBe(true);
    const me = await env.c.players.getSelf(p.id);
    expect(me.ok && me.value.totalStars).toBe(3);
    const gained = results.reduce((sum, r) => sum + (r.ok ? r.value.coinsGained : 0), 0);
    expect(gained).toBe(10 + 2 + 3 * 5); // bir kez: bölüm ödülü + 3 yıldız
  });

  it("günlük ödül paralel istekte yalnızca bir kez alınır", async () => {
    const env = await createEnv();
    const p = await newPlayer(env);
    const results = await Promise.all(Array.from({ length: 6 }, () => env.c.rewards.claimDaily(p.id)));
    expect(results.filter((r) => r.ok)).toHaveLength(1);
    const me = await env.c.players.getSelf(p.id);
    expect(me.ok && me.value.coins).toBe(50);
  });

  it("tek ürünlük coin ile iki ürün paralel satın alınamaz (çift harcama yok)", async () => {
    const env = await createEnv();
    const p = await newPlayer(env);
    await env.c.rewards.grant(p.id, 0, 150, "test", null); // hat_crown = 150
    const [a, b] = await Promise.all([env.c.rewards.buy(p.id, "hat_crown"), env.c.rewards.buy(p.id, "glasses_star")]);
    expect([a.ok, b.ok].filter(Boolean)).toHaveLength(1);
    const me = await env.c.players.getSelf(p.id);
    expect(me.ok && me.value.coins).toBeGreaterThanOrEqual(0);
  });

  it("bakiye asla negatif olmaz", async () => {
    const env = await createEnv();
    const p = await newPlayer(env);
    expect(await env.store.spendCoins(p.id, 10, "t", null)).toBeNull();
    expect(await env.store.addCoins(p.id, -999, "t", null)).toBe(0);
  });
});
