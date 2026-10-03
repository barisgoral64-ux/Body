import { describe, expect, it } from "vitest";
import { compareVersions, parseVersion } from "../src/domain/version.js";
import { loadConfig } from "../src/config/env.js";
import { buildApp } from "../src/app.js";
import { createContainer } from "../src/container.js";
import { createLogger } from "../src/infra/logger.js";
import { MemoryStore } from "../src/infra/memoryStore.js";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const make = (env: Record<string, string>) =>
  buildApp(createContainer(loadConfig(env), createLogger("error", "t", () => undefined), { store: new MemoryStore() })).app;

describe("sürüm karşılaştırma", () => {
  it("ayrıştırır ve karşılaştırır", () => {
    expect(parseVersion("1.2.3")).toEqual([1, 2, 3]);
    expect(parseVersion("0.1.0-test")).toEqual([0, 1, 0]);
    expect(parseVersion("1.2")).toBeNull();
    expect(parseVersion("a.b.c")).toBeNull();
    expect(compareVersions("1.2.3", "1.2.3")).toBe(0);
    expect(compareVersions("1.2.9", "1.10.0")).toBe(-1); // sayısal, metin sırası değil
    expect(compareVersions("2.0.0", "1.99.99")).toBe(1);
    expect(compareVersions("1.0.0-test", "1.0.0")).toBe(0);
    expect(compareVersions("x", "1.0.0")).toBeNull();
  });
  it("yapılandırma geçersiz veya ters sürümü reddeder", () => {
    expect(() => loadConfig({ APP_MIN_VERSION: "abc" })).toThrow(/sürüm/);
    expect(() => loadConfig({ APP_MIN_VERSION: "2.0.0", APP_LATEST_VERSION: "1.0.0" })).toThrow(/büyük/);
  });
});

describe("güncelleme politikası (HTTP)", () => {
  it("/v1/app-version kimliksiz döner", async () => {
    const app = make({ APP_MIN_VERSION: "1.1.0", APP_LATEST_VERSION: "1.3.0", APP_UPDATE_URL: "https://play.google.com/store/apps/details?id=x" });
    const res = await app.inject({ method: "GET", url: "/v1/app-version" });
    expect(res.statusCode).toBe(200);
    expect(res.json()).toEqual({ minSupportedVersion: "1.1.0", latestVersion: "1.3.0", updateUrl: "https://play.google.com/store/apps/details?id=x" });
    // İstemci DTO testi bu fixture'ı okur: sunucu yanıtı değişirse ikisi birlikte güncellenmeli.
    const fixture = JSON.parse(readFileSync(join(dirname(fileURLToPath(import.meta.url)), "..", "..", "protocol", "app-version.json"), "utf8"));
    expect(res.json()).toEqual(fixture);
    await app.close();
  });
  it("asgari sürümün altındaki istemci 426 alır; yeterli, sürümsüz ve sürüm uç noktası geçer", async () => {
    const app = make({ APP_MIN_VERSION: "1.1.0", APP_LATEST_VERSION: "1.3.0" });
    const old = await app.inject({ method: "POST", url: "/v1/auth/anonymous", headers: { "x-app-version": "1.0.9" }, payload: { deviceId: "version-test-device-0001" } });
    expect(old.statusCode).toBe(426);
    expect(old.json()).toEqual({ code: "PROTOCOL_MISMATCH" });
    const ok = await app.inject({ method: "POST", url: "/v1/auth/anonymous", headers: { "x-app-version": "1.1.0" }, payload: { deviceId: "version-test-device-0002" } });
    expect(ok.statusCode).toBe(200);
    const none = await app.inject({ method: "POST", url: "/v1/auth/anonymous", payload: { deviceId: "version-test-device-0003" } });
    expect(none.statusCode).toBe(200);
    const check = await app.inject({ method: "GET", url: "/v1/app-version", headers: { "x-app-version": "0.0.1" } });
    expect(check.statusCode).toBe(200); // eski istemci de güncelleme bilgisini alabilmeli
    await app.close();
  });
});
