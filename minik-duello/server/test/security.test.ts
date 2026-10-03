import type { IncomingMessage } from "node:http";
import type { AddressInfo } from "node:net";
import { afterEach, describe, expect, it } from "vitest";
import WebSocket from "ws";
import { buildApp } from "../src/app.js";
import { LIMITS, PARENT } from "../src/config/constants.js";
import { loadConfig } from "../src/config/env.js";
import { createContainer } from "../src/container.js";
import { createLogger } from "../src/infra/logger.js";
import { FakeTime } from "../src/infra/runtime.js";
import { clientIp } from "../src/transport/ws/gateway.js";
import { ErrorCode } from "../src/shared/result.js";
import { createEnv, createTestStore, makeFriends, newPlayer, PIN, seededRandom, type TestEnv } from "./helpers.js";
import { MemoryStore } from "../src/infra/memoryStore.js";

const cleanups: (() => Promise<void> | void)[] = [];
afterEach(async () => {
  for (const c of cleanups.splice(0).reverse()) await c();
});

async function assertErased(env: TestEnv, id: string): Promise<void> {
  expect(await env.store.getPlayer(id)).toBeNull();
  expect(await env.store.getProfile(id)).toBeNull();
  expect(await env.store.getSettings(id)).toBeNull();
  expect(await env.store.listInventory(id)).toHaveLength(0);
  expect(await env.store.listProgress(id)).toHaveLength(0);
  expect(await env.store.getSave(id)).toBeNull();
  expect(await env.store.getDailyClaim(id)).toBeNull();
  expect(await env.store.listFriendIds(id)).toHaveLength(0);
  expect(await env.store.listBlocked(id)).toHaveLength(0);
  if (env.store instanceof MemoryStore) expect(env.store.hasAnyDataFor(id)).toBe(false);
}

describe("hesap silme (çocuk verisi silme hakkı)", () => {
  it("yanlış PIN ile silinemez; doğru PIN ile tüm veri silinir, diğer oyuncu etkilenmez", async () => {
    const env = await createEnv();
    const a = await newPlayer(env);
    const b = await newPlayer(env);
    await makeFriends(env, a, b);
    await env.c.levels.submitResult(a.id, 1, 500, 20_000);
    await env.c.saves.save(a.id, { music: 0.4 });
    await env.c.rewards.claimDaily(a.id);
    await env.c.parent.blockPlayer(b.id, a.id, PIN);

    expect((await env.c.parent.deleteAccount(a.id, "0000")).ok).toBe(false);
    expect(await env.store.getPlayer(a.id)).not.toBeNull();

    expect((await env.c.parent.deleteAccount(a.id, PIN)).ok).toBe(true);
    await assertErased(env, a.id);

    // Diğer oyuncunun hesabı ve verisi yerinde; silinen oyuncu listesinde yok.
    expect(await env.store.getPlayer(b.id)).not.toBeNull();
    expect(await env.store.listFriendIds(b.id)).toEqual([]);
    expect(await env.store.listBlocked(b.id)).toEqual([]);
    const lb = await env.c.leaderboard.weeklyAmongFriends(b.id);
    expect(lb.ok && lb.value.map((e) => e.playerId)).toEqual([b.id]);
  });

  it("silinen hesabın token'ı artık işe yaramaz; aynı cihaz yeni (boş) hesap açar", async () => {
    const env = await createEnv();
    const a = await newPlayer(env);
    await env.c.parent.setPin(a.id, PIN);
    await env.c.parent.deleteAccount(a.id, PIN);
    const me = await env.c.players.getSelf(a.id);
    expect(me.ok === false && me.error.code).toBe(ErrorCode.NotFound);
    const refreshed = await env.c.auth.registerAnonymous("test-device-00000001-abcdefgh");
    expect(refreshed.ok && refreshed.value.player.playerId).not.toBe(a.id);
  });

  it("silinen oyuncunun maç kaydı anonimleşir, rakibin geçmişi korunur", async () => {
    const env = await createEnv();
    const a = await newPlayer(env);
    const b = await newPlayer(env);
    const room = env.c.rooms.createRoom(a.id, b.id, "colorRace");
    if (!room.ok) throw new Error("oda");
    room.value.setReady(a.id);
    room.value.setReady(b.id);
    env.time.advance(3000);
    room.value.leave(a.id);
    await room.value.persisted;
    expect(await env.matchCount()).toBe(1);
    await env.c.parent.setPin(a.id, PIN);
    expect((await env.c.parent.deleteAccount(a.id, PIN)).ok).toBe(true);
    expect(await env.matchCount()).toBe(1);
    await assertErased(env, a.id);
  });
});

describe("PIN kaba kuvvet koruması", () => {
  it("kilitlenme süresi her ardışık kilitlenmede büyür, başarılı girişte sıfırlanır", async () => {
    const env = await createEnv();
    const p = await newPlayer(env);
    await env.c.parent.setPin(p.id, PIN);

    const fail = async () => {
      for (let i = 0; i < PARENT.maxPinAttempts; i += 1) await env.c.parent.verifyPin(p.id, "0000");
    };
    await fail();
    let s = await env.store.getSettings(p.id);
    expect(s?.pinLockedUntil?.getTime()).toBe(env.time.now().getTime() + PARENT.pinLockoutMs);
    expect((await env.c.parent.verifyPin(p.id, PIN)).ok).toBe(false); // kilitliyken doğru PIN de geçmez

    env.time.advance(PARENT.pinLockoutMs + 1);
    await fail();
    s = await env.store.getSettings(p.id);
    expect(s?.pinLockedUntil?.getTime()).toBe(env.time.now().getTime() + PARENT.pinLockoutMs * PARENT.pinLockoutGrowth);

    env.time.advance(PARENT.pinLockoutMs * PARENT.pinLockoutGrowth + 1);
    expect((await env.c.parent.verifyPin(p.id, PIN)).ok).toBe(true);
    s = await env.store.getSettings(p.id);
    expect(s?.pinLockoutCount).toBe(0);
  });

  it("kilitlenme süresi üst sınırı aşmaz", async () => {
    const env = await createEnv();
    const p = await newPlayer(env);
    await env.c.parent.setPin(p.id, PIN);
    for (let round = 0; round < 12; round += 1) {
      for (let i = 0; i < PARENT.maxPinAttempts; i += 1) await env.c.parent.verifyPin(p.id, "0000");
      const s = await env.store.getSettings(p.id);
      expect((s?.pinLockedUntil?.getTime() ?? 0) - env.time.now().getTime()).toBeLessThanOrEqual(PARENT.pinLockoutMaxMs);
      env.time.advance(PARENT.pinLockoutMaxMs + 1);
    }
  });
});

describe("ağ güvenliği", () => {
  it("clientIp: X-Forwarded-For yalnızca güvenilen proxy ile kullanılır", () => {
    const req = (xff?: string): IncomingMessage =>
      ({ headers: xff ? { "x-forwarded-for": xff } : {}, socket: { remoteAddress: "10.0.0.5" } }) as unknown as IncomingMessage;
    expect(clientIp(req("203.0.113.7, 10.0.0.1"), false)).toBe("10.0.0.5"); // sahte başlığa güvenme
    expect(clientIp(req("203.0.113.7, 10.0.0.1"), true)).toBe("203.0.113.7");
    expect(clientIp(req(), true)).toBe("10.0.0.5");
  });

  it("HTTP yanıtlarında güvenlik başlıkları var", async () => {
    const env = await createEnv();
    const { app } = buildApp(env.c);
    const res = await app.inject({ method: "GET", url: "/health" });
    expect(res.headers["x-content-type-options"]).toBe("nosniff");
    expect(res.headers["cache-control"]).toBe("no-store");
    await app.close();
  });

  it("aynı IP'den çok sayıda eşzamanlı WebSocket reddedilir; hesap silinince bağlantı kapanır", async () => {
    const time = new FakeTime();
    const container = createContainer(loadConfig({}), createLogger("error", "t", () => undefined), {
      store: (await createTestStore()).store, clock: time, scheduler: time, random: seededRandom(2),
    });
    const { app } = buildApp(container);
    await app.listen({ port: 0, host: "127.0.0.1" });
    cleanups.push(() => app.close());
    const port = (app.server.address() as AddressInfo).port;

    const sockets: WebSocket[] = [];
    cleanups.push(() => sockets.forEach((s) => s.terminate()));
    for (let i = 0; i < LIMITS.wsConnectionsPerIp; i += 1) {
      const ws = new WebSocket(`ws://127.0.0.1:${port}/ws`);
      sockets.push(ws);
      await new Promise<void>((resolve, reject) => {
        ws.on("open", () => resolve());
        ws.on("error", reject);
      });
    }
    const extra = new WebSocket(`ws://127.0.0.1:${port}/ws`);
    const rejected = await new Promise<boolean>((resolve) => {
      extra.on("open", () => resolve(false));
      extra.on("error", () => resolve(true));
      extra.on("unexpected-response", () => resolve(true));
    });
    expect(rejected).toBe(true);

    // Hesap silinince canlı bağlantı kapatılır.
    for (const s of sockets) s.terminate();
    await new Promise((r) => setTimeout(r, 50));
    const reg = await container.auth.registerAnonymous("security-test-device-0000000001");
    if (!reg.ok) throw new Error("kayıt");
    await container.parent.setPin(reg.value.player.playerId, PIN);
    const ws = new WebSocket(`ws://127.0.0.1:${port}/ws`);
    sockets.push(ws);
    await new Promise<void>((resolve) => ws.on("open", () => resolve()));
    ws.send(JSON.stringify({ v: 1, type: "auth", payload: { token: reg.value.tokens.accessToken } }));
    await new Promise((resolve) => ws.once("message", resolve));
    const closed = new Promise<number>((resolve) => ws.on("close", (code) => resolve(code)));
    const res = await app.inject({
      method: "POST", url: "/v1/parent/delete-account", payload: { pin: PIN, confirm: true },
      headers: { authorization: `Bearer ${reg.value.tokens.accessToken}` },
    });
    expect(res.statusCode).toBe(200);
    expect(await closed).toBe(4001);
  });
});
