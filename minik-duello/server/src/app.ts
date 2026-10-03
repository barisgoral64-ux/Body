import Fastify, { type FastifyInstance } from "fastify";
import { LIMITS, PROTOCOL_VERSION } from "./config/constants.js";
import type { AppConfig } from "./config/env.js";
import type { Logger } from "./infra/logger.js";

/** Fastify uygulaması; testte main.ts olmadan oluşturulabilir. */
export function buildApp(config: AppConfig, logger: Logger): FastifyInstance {
  const app = Fastify({
    logger: false,
    bodyLimit: LIMITS.wsMaxMessageBytes * 8,
  });

  app.addHook("onResponse", (request, reply, done) => {
    logger.debug("http", { method: request.method, url: request.url, status: reply.statusCode });
    done();
  });

  app.setErrorHandler((error, request, reply) => {
    logger.error("http_error", { url: request.url, message: error instanceof Error ? error.message : "unknown" });
    // İç ayrıntı istemciye sızdırılmaz.
    void reply.status(500).send({ code: "INTERNAL" });
  });

  app.get("/health", () => ({
    status: "ok",
    environment: config.environment,
    protocolVersion: PROTOCOL_VERSION,
  }));

  return app;
}
