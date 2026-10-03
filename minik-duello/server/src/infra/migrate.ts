import { readdirSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import type { Pool } from "pg";
import { buildLevelCatalog } from "../domain/levels.js";
import { REWARD_CATALOG } from "../domain/rewards.js";

const MIGRATIONS_DIR = join(dirname(fileURLToPath(import.meta.url)), "..", "..", "migrations");

/** Sıralı SQL göçleri; her dosya bir kez, tek işlemde uygulanır. */
export async function runMigrations(pool: Pool, dir: string = MIGRATIONS_DIR): Promise<string[]> {
  await pool.query("CREATE TABLE IF NOT EXISTS schema_migrations (name TEXT PRIMARY KEY, applied_at TIMESTAMPTZ NOT NULL DEFAULT now())");
  const applied = new Set((await pool.query<{ name: string }>("SELECT name FROM schema_migrations")).rows.map((r) => r.name));
  const ran: string[] = [];
  for (const file of readdirSync(dir).filter((f) => f.endsWith(".sql")).sort()) {
    if (applied.has(file)) continue;
    const client = await pool.connect();
    try {
      // Dosyanın kendi BEGIN/COMMIT'i vardır; kayıt ayrı ve idempotenttir.
      await client.query(readFileSync(join(dir, file), "utf8"));
      await client.query("INSERT INTO schema_migrations(name) VALUES ($1) ON CONFLICT DO NOTHING", [file]);
      ran.push(file);
    } finally {
      client.release();
    }
  }
  return ran;
}

/** Katalog verisi koddan gelir; DB'deki yabancı anahtarlar için başlangıçta eşitlenir. */
export async function seedStaticData(pool: Pool): Promise<void> {
  for (const r of REWARD_CATALOG) {
    await pool.query(
      "INSERT INTO rewards(reward_id, type, name_key) VALUES ($1,$2,$3) ON CONFLICT (reward_id) DO UPDATE SET type=EXCLUDED.type",
      [r.id, r.type, r.id],
    );
  }
  for (const l of buildLevelCatalog()) {
    await pool.query(
      `INSERT INTO levels(level_id, world_id, game_type, difficulty, config, next_level_id) VALUES ($1,$2,$3,$4,$5,NULL)
       ON CONFLICT (level_id) DO UPDATE SET game_type=EXCLUDED.game_type, difficulty=EXCLUDED.difficulty, config=EXCLUDED.config`,
      [l.levelID, l.worldID, l.gameType, l.difficulty, JSON.stringify(l)],
    );
  }
  // next_level_id kendi tablosuna işaret ettiğinden satırlar eklendikten sonra bağlanır.
  await pool.query("UPDATE levels SET next_level_id = level_id + 1 WHERE level_id < (SELECT MAX(level_id) FROM levels)");
}
