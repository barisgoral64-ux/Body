import Fastify, { type FastifyInstance } from "fastify";
import { LIMITS, PROTOCOL_VERSION } from "./config/constants.js";
import type { Container } from "./container.js";
import { Gateway } from "./transport/ws/gateway.js";
import { registerRoutes } from "./transport/http/routes.js";

export interface BuiltApp {
  readonly app: FastifyInstance;
  readonly gateway: Gateway;
}

/** Fastify uygulaması + WebSocket geçidi; testte main.ts olmadan oluşturulabilir. */
export function buildApp(c: Container): BuiltApp {
  const app = Fastify({ logger: false, bodyLimit: LIMITS.wsMaxMessageBytes * 8, trustProxy: c.config.trustProxy });

  app.addHook("onResponse", (request, reply, done) => {
    c.logger.debug("http", { method: request.method, url: request.url, status: reply.statusCode });
    done();
  });

  app.setErrorHandler((error, request, reply) => {
    c.logger.error("http_error", { url: request.url, message: error instanceof Error ? error.message : "unknown" });
    // İç ayrıntı istemciye sızdırılmaz.
    const status = (error as { statusCode?: number }).statusCode;
    void reply.status(status && status >= 400 && status < 500 ? status : 500).send({ code: status && status < 500 ? "INVALID_INPUT" : "INTERNAL" });
  });

  app.addHook("onSend", (_request, reply, _payload, done) => {
    void reply.header("x-content-type-options", "nosniff").header("cache-control", "no-store");
    done();
  });

  app.get("/health", () => ({
    status: "ok",
    environment: c.config.environment,
    protocolVersion: PROTOCOL_VERSION,
  }));

  registerRoutes(app, c);

  const gateway = new Gateway(c);
  app.addHook("onReady", () => {
    gateway.attach(app.server);
  });
  app.addHook("onClose", () => {
    gateway.close();
  });
  return { app, gateway };
}
