import { Pool } from "pg";
import { buildApp } from "./app.js";
import { loadConfig } from "./config/env.js";
import { createContainer } from "./container.js";
import { createLogger } from "./infra/logger.js";
import { MemoryStore } from "./infra/memoryStore.js";
import { runMigrations, seedStaticData } from "./infra/migrate.js";
import { PgStore } from "./infra/pgStore.js";
import type { DataStore } from "./infra/store.js";

const HOUSEKEEPING_INTERVAL_MS = 60 * 60 * 1000;
const DB_POOL_MAX = 10;

async function openStore(
  config: ReturnType<typeof loadConfig>,
  logger: ReturnType<typeof createLogger>,
): Promise<{ store: DataStore; close: () => Promise<void> }> {
  if (!config.databaseUrl) {
    if (config.isProduction) throw new Error("Üretimde DATABASE_URL zorunludur.");
    logger.warn("memory_store", { note: "DATABASE_URL yok: veriler süreç kapanınca silinir (yalnızca geliştirme)." });
    return { store: new MemoryStore(), close: () => Promise.resolve() };
  }
  const poolConfig = { connectionString: config.databaseUrl, max: DB_POOL_MAX };
  const pool = new Pool(poolConfig);
  const lockPool = new Pool(poolConfig);
  const applied = await runMigrations(pool);
  await seedStaticData(pool);
  logger.info("database_ready", { migrationsApplied: applied.length });
  return { store: new PgStore(pool, lockPool), close: async () => { await pool.end(); await lockPool.end(); } };
}

async function main(): Promise<void> {
  const config = loadConfig();
  const logger = createLogger(config.logLevel);
  const { store, close } = await openStore(config, logger);

  const container = createContainer(config, logger, { store });
  const { app } = buildApp(container);

  // Süresi dolan arkadaş isteklerini periyodik temizler.
  const housekeeping = setInterval(() => {
    container.friendRequests.expireOverdue().catch((error: unknown) => {
      logger.error("housekeeping_failed", { message: error instanceof Error ? error.message : "unknown" });
    });
  }, HOUSEKEEPING_INTERVAL_MS);

  const shutdown = async (signal: string): Promise<void> => {
    logger.info("shutdown", { signal });
    clearInterval(housekeeping);
    await app.close();
    await close();
    process.exit(0);
  };
  process.on("SIGINT", () => void shutdown("SIGINT"));
  process.on("SIGTERM", () => void shutdown("SIGTERM"));

  await app.listen({ port: config.port, host: config.host });
  logger.info("server_started", { port: config.port, environment: config.environment });
}

main().catch((error: unknown) => {
  process.stderr.write(`Başlatma hatası: ${error instanceof Error ? error.message : String(error)}\n`);
  process.exit(1);
});
