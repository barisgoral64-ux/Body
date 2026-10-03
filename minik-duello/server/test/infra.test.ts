import { describe, expect, it } from "vitest";
import { buildApp } from "../src/app.js";
import { createContainer } from "../src/container.js";
import { MemoryStore } from "../src/infra/memoryStore.js";
import { loadConfig } from "../src/config/env.js";
import { createLogger, maskPlayerId } from "../src/infra/logger.js";

describe("config", () => {
  it("üretimde zayıf JWT_SECRET reddedilir", () => {
    expect(() => loadConfig({ NODE_ENV: "production", JWT_SECRET: "kisa" })).toThrow();
  });
  it("üretimde güçlü secret ile açılır ve log seviyesi info olur", () => {
    const c = loadConfig({ NODE_ENV: "production", JWT_SECRET: "x".repeat(40), DATABASE_URL: "postgres://x/y" });
    expect(c.isProduction).toBe(true);
    expect(c.logLevel).toBe("info");
  });
  it("geliştirmede varsayılanlarla açılır", () => {
    const c = loadConfig({});
    expect(c.environment).toBe("development");
    expect(c.logLevel).toBe("debug");
  });
  it("üretimde DATABASE_URL zorunlu", () => {
    expect(() => loadConfig({ NODE_ENV: "production", JWT_SECRET: "x".repeat(40) })).toThrow(/DATABASE_URL/);
  });
  it("geçersiz PORT reddedilir", () => {
    expect(() => loadConfig({ PORT: "abc" })).toThrow();
  });
});

describe("logger", () => {
  it("hassas alanları gizler ve seviye filtreler", () => {
    const lines: string[] = [];
    const log = createLogger("info", "t", (l) => lines.push(l));
    log.debug("görünmez");
    log.info("giriş", { token: "SIR", pin: "1234", ok: 1 });
    expect(lines).toHaveLength(1);
    expect(lines[0]).not.toContain("SIR");
    expect(lines[0]).not.toContain("1234");
    expect(lines[0]).toContain("\"ok\":1");
  });
  it("oyuncu ID maskelenir", () => {
    expect(maskPlayerId("abcdef123456")).toBe("abcd…");
  });
});

describe("http", () => {
  it("/health çalışır", async () => {
    const logger = createLogger("error", "t", () => undefined);
    const { app } = buildApp(createContainer(loadConfig({}), logger, { store: new MemoryStore() }));
    const res = await app.inject({ method: "GET", url: "/health" });
    expect(res.statusCode).toBe(200);
    expect(res.json()).toMatchObject({ status: "ok", protocolVersion: 1 });
    await app.close();
  });
});
