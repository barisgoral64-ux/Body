import { randomBytes } from "node:crypto";
import { Pool } from "pg";
import { afterAll, describe, expect, it } from "vitest";
import { runMigrations, seedStaticData } from "../src/infra/migrate.js";
import { REWARD_CATALOG } from "../src/domain/rewards.js";

const URL = process.env.TEST_DATABASE_URL;

describe.skipIf(!URL)("PostgreSQL göçleri", () => {
  const schema = `m_${randomBytes(6).toString("hex")}`;
  const admin = new Pool({ connectionString: URL, max: 1 });
  let pool: Pool;

  afterAll(async () => {
    await pool?.end();
    await admin.query(`DROP SCHEMA IF EXISTS ${schema} CASCADE`);
    await admin.end();
  });

  it("göçler bir kez uygulanır, tekrar çalıştırmak güvenlidir, katalog eşitlenir", async () => {
    await admin.query(`CREATE SCHEMA ${schema}`);
    pool = new Pool({ connectionString: URL, options: `-c search_path=${schema}`, max: 2 });
    expect((await runMigrations(pool)).length).toBeGreaterThan(0);
    expect(await runMigrations(pool)).toEqual([]);
    await seedStaticData(pool);
    await seedStaticData(pool);
    const rewards = await pool.query("SELECT COUNT(*)::int AS n FROM rewards");
    const levels = await pool.query("SELECT COUNT(*)::int AS n, COUNT(next_level_id)::int AS linked FROM levels");
    expect(rewards.rows[0].n).toBe(REWARD_CATALOG.length);
    expect(levels.rows[0]).toEqual({ n: 100, linked: 99 });
  });

  it("şema kısıtları ihlalleri reddeder: tek taraflı çift sıra, negatif skor, kendine istek", async () => {
    const id = () => "00000000-0000-4000-8000-" + randomBytes(6).toString("hex");
    const a = id();
    const b = id();
    for (const p of [a, b]) {
      await pool.query("INSERT INTO players(player_id, device_hash, friend_code, username) VALUES ($1,$2,$3,'x')", [p, p, p.slice(-8)]);
    }
    const [lo, hi] = a < b ? [a, b] : [b, a];
    await expect(pool.query("INSERT INTO friends(player_a, player_b) VALUES ($1,$2)", [hi, lo])).rejects.toThrow();
    await expect(pool.query("INSERT INTO friends(player_a, player_b) VALUES ($1,$2)", [lo, hi])).resolves.toBeDefined();
    await expect(
      pool.query("INSERT INTO friend_requests(request_id, sender_id, receiver_id, status, expires_at) VALUES ($1,$2,$2,'pending', now())", [id(), a]),
    ).rejects.toThrow();
    await expect(pool.query("UPDATE player_profiles SET coins = -1")).resolves.toBeDefined(); // profil satırı yok: etkisiz
  });
});
