import { buildApp } from "./app.js";
import { loadConfig } from "./config/env.js";
import { createLogger } from "./infra/logger.js";

async function main(): Promise<void> {
  const config = loadConfig();
  const logger = createLogger(config.logLevel);
  const app = buildApp(config, logger);

  const shutdown = async (signal: string): Promise<void> => {
    logger.info("shutdown", { signal });
    await app.close();
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
