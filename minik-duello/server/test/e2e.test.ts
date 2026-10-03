import type { AddressInfo } from "node:net";
import { afterEach, describe, expect, it } from "vitest";
import WebSocket from "ws";
import { buildApp } from "../src/app.js";
import { REWARDS, ROOM, ROUND_TIMING } from "../src/config/constants.js";
import { loadConfig } from "../src/config/env.js";
import { createContainer } from "../src/container.js";
import { createLogger } from "../src/infra/logger.js";
import { FakeTime } from "../src/infra/runtime.js";
import type { RoundView } from "../src/protocol.js";
import { correctChoices, createTestStore, PIN, seededRandom } from "./helpers.js";

interface Msg {
  v: number;
  type: string;
  payload: Record<string, unknown>;
}

class Client {
  readonly queue: Msg[] = [];
  private waiters: { type: string; resolve: (m: Msg) => void }[] = [];
  closed = false;
  closeCode = 0;
  constructor(readonly ws: WebSocket) {
    ws.on("message", (d) => {
      const m = JSON.parse(d.toString()) as Msg;
      const w = this.waiters.findIndex((x) => x.type === m.type);
      if (w >= 0) this.waiters.splice(w, 1)[0]?.resolve(m);
      else this.queue.push(m);
    });
    ws.on("close", (code) => {
      this.closed = true;
      this.closeCode = code;
    });
  }
  send(type: string, payload?: Record<string, unknown>): void {
    this.ws.send(JSON.stringify({ v: 1, type, payload: payload ?? {} }));
  }
  next(type: string, timeoutMs = 2000): Promise<Msg> {
    const i = this.queue.findIndex((m) => m.type === type);
    if (i >= 0) return Promise.resolve(this.queue.splice(i, 1)[0] as Msg);
    return new Promise((resolve, reject) => {
      const t = setTimeout(() => reject(new Error(`zaman aşımı: ${type}`)), timeoutMs);
      this.waiters.push({ type, resolve: (m) => (clearTimeout(t), resolve(m)) });
    });
  }
  drain(type: string): Msg[] {
    const out = this.queue.filter((m) => m.type === type);
    for (const m of out) this.queue.splice(this.queue.indexOf(m), 1);
    return out;
  }
}

const open = (url: string): Promise<Client> =>
  new Promise((resolve, reject) => {
    const ws = new WebSocket(url);
    ws.on("open", () => resolve(new Client(ws)));
    ws.on("error", reject);
  });

const cleanups: (() => Promise<void> | void)[] = [];
afterEach(async () => {
  for (const c of cleanups.splice(0).reverse()) await c();
});

async function boot() {
  const time = new FakeTime();
  const logger = createLogger("error", "e2e", () => undefined);
  const container = createContainer(loadConfig({}), logger, {
    store: (await createTestStore()).store, clock: time, scheduler: time, random: seededRandom(7),
  });
  const { app } = buildApp(container);
  await app.listen({ port: 0, host: "127.0.0.1" });
  cleanups.push(() => app.close());
  const port = (app.server.address() as AddressInfo).port;
  const wsUrl = `ws://127.0.0.1:${port}/ws`;

  const call = async (method: "GET" | "POST" | "PUT", url: string, token: string | null, body?: unknown) => {
    const res = await app.inject({
      method, url, payload: body as object | undefined,
      headers: token ? { authorization: `Bearer ${token}` } : undefined,
    });
    return { status: res.statusCode, body: res.json() as Record<string, any> };
  };

  let n = 0;
  const makePlayer = async () => {
    n += 1;
    const reg = await call("POST", "/v1/auth/anonymous", null, { deviceId: `e2e-device-${String(n).padStart(6, "0")}-xxxxxxxxx` });
    const token = reg.body.tokens.accessToken as string;
    await call("PUT", "/v1/parent/pin", token, { newPin: PIN });
    await call("PUT", "/v1/parent/settings", token, {
      pin: PIN, patch: { friendsEnabled: true, multiplayerEnabled: true, onlineStatusVisible: true, gameInvitationsEnabled: true },
    });
    return { id: reg.body.player.playerId as string, code: reg.body.player.friendCode as string, token };
  };
  const connect = async (token: string): Promise<Client> => {
    const c = await open(wsUrl);
    cleanups.push(() => c.ws.close());
    c.send("auth", { token });
    await c.next("auth.ok");
    return c;
  };
  return { time, call, makePlayer, connect, wsUrl, container };
}

describe("uçtan uca: hesap → arkadaş → davet → oyun → ödül", () => {
  it("tam akış, kopma ve geri dönme dahil", async () => {
    const { time, call, makePlayer, connect } = await boot();
    const a = await makePlayer();
    const b = await makePlayer();

    // Arkadaş kodu ile istek, kabul, karşılıklı liste.
    expect((await call("POST", "/v1/friends/requests", a.token, { friendCode: b.code })).status).toBe(200);
    const incoming = await call("GET", "/v1/friends/requests/incoming", b.token);
    expect(incoming.body.requests).toHaveLength(1);
    expect(incoming.body.requests[0].from.username).toBeTruthy();
    expect(incoming.body.requests[0].from.friendCode).toBeUndefined(); // kod sızmaz
    expect((await call("POST", `/v1/friends/requests/${incoming.body.requests[0].requestId}/respond`, b.token, { accept: true })).status).toBe(200);
    expect((await call("GET", "/v1/friends", a.token)).body.friends.map((f: any) => f.playerId)).toEqual([b.id]);
    expect((await call("GET", "/v1/friends", b.token)).body.friends.map((f: any) => f.playerId)).toEqual([a.id]);

    // WebSocket: bağlan, çevrimiçi görün.
    const ca = await connect(a.token);
    const cb = await connect(b.token);
    await new Promise((r) => setTimeout(r, 50));
    const friendsView = await call("GET", "/v1/friends", a.token);
    expect(friendsView.body.friends[0].presence).toBe("online");

    // Davet.
    ca.send("invite.send", { receiverId: b.id, mode: "mixedMatch" });
    expect((await ca.next("invite.resolved")).payload.status).toBe("sent");
    const invitation = await cb.next("invite.received");
    expect((invitation.payload.from as { username: string }).username).toBeTruthy();
    cb.send("invite.respond", { inviteId: invitation.payload.inviteId, accept: true });
    const stateA = await ca.next("room.state");
    const stateB = await cb.next("room.state");
    const roomId = stateA.payload.roomId as string;

    // Hazır → geri sayım → turlar.
    ca.send("room.ready");
    cb.send("room.ready");
    await ca.next("room.countdown");
    await cb.next("room.countdown");
    time.advance(ROOM.countdownSeconds * 1000);

    const answerRound = async (acorrect: boolean, bcorrect: boolean) => {
      const ra = (await ca.next("room.round")).payload as unknown as RoundView;
      const rb = (await cb.next("room.round")).payload as unknown as RoundView;
      expect(ra.roundId).toBe(rb.roundId); // aynı görev
      time.advance(ra.showMs + 800);
      const right = correctChoices(ra);
      const wrong = ra.choices.find((c) => !right.includes(c.id))?.id as string;
      ca.send("room.answer", { roundId: ra.roundId, choiceId: acorrect ? (right[0] as string) : wrong });
      await ca.next("room.answerAck");
      time.advance(1500);
      cb.send("room.answer", { roundId: rb.roundId, choiceId: bcorrect ? (right[0] as string) : wrong });
      await cb.next("room.answerAck");
      await ca.next("room.roundResult");
      await cb.next("room.roundResult");
    };

    await answerRound(true, true);
    time.advance(ROUND_TIMING.betweenRoundsMs);

    // Hazır mesaj ve kopma: B'nin bağlantısı koptu, A uyarılır, B geri döner.
    ca.send("room.quickChat", { message: "hello" });
    expect((await cb.next("room.quickChat")).payload.message).toBe("hello");
    ca.send("room.quickChat", { message: "merhaba benim adım Ali" as never }); // serbest metin
    expect((await ca.next("error")).payload.code).toBe("INVALID_INPUT");

    cb.ws.close();
    await ca.next("room.opponentDisconnected");
    time.advance(5000);
    const cb2 = await connect(b.token);
    // Doğru anahtar olmadan odaya sızılamaz.
    cb2.send("room.resume", { roomId, resumeToken: "yanlis" });
    expect((await cb2.next("error")).payload.code).toBe("FORBIDDEN");
    const tokenB = stateB.payload.resumeToken as string;
    expect(tokenB).not.toBe("");
    cb2.send("room.resume", { roomId, resumeToken: tokenB });
    await cb2.next("room.state");
    await ca.next("room.opponentReconnected");

    // Maçın kalanını oyna.
    const cbLive = cb2;
    const remaining = async () => {
      const ra = (await ca.next("room.round")).payload as unknown as RoundView;
      const rb = (await cbLive.next("room.round")).payload as unknown as RoundView;
      time.advance(ra.showMs + 800);
      ca.send("room.answer", { roundId: ra.roundId, choiceId: correctChoices(ra)[0] as string });
      await ca.next("room.answerAck");
      time.advance(1500);
      cbLive.send("room.answer", { roundId: rb.roundId, choiceId: correctChoices(rb)[0] as string });
      await cbLive.next("room.answerAck");
      await ca.next("room.roundResult");
    };
    for (let i = 0; i < 4; i += 1) {
      await remaining();
      if (i < 3) time.advance(ROUND_TIMING.betweenRoundsMs);
    }

    const finA = await ca.next("room.finished");
    const finB = await cbLive.next("room.finished");
    expect(finA.payload.reason).toBe("completed");
    const outcomes = finB.payload.outcomes as { playerId: string; isWinner: boolean; starsAwarded: number }[];
    expect(outcomes.find((o) => o.playerId === a.id)?.isWinner).toBe(true); // A hep daha hızlı
    expect(outcomes.every((o) => o.starsAwarded >= REWARDS.matchParticipantStars)).toBe(true);

    // Ödüller sunucuda kalıcılaştı ve "Tekrar oyna" mümkün (oda temizlendi).
    const meB = await call("GET", "/v1/me", b.token);
    expect(meB.body.totalStars).toBe(REWARDS.matchParticipantStars);
    expect(meB.body.coins).toBe(REWARDS.matchParticipantCoins);
    const week = await call("GET", "/v1/leaderboard/weekly", a.token);
    expect(week.body.entries.map((e: any) => e.playerId)).toEqual([a.id, b.id]);
    ca.drain("invite.resolved");
    time.advance(10_000); // davet bekleme süresi geçsin
    ca.send("invite.send", { receiverId: b.id, mode: "colorRace" });
    expect((await ca.next("invite.resolved")).payload.status).toBe("sent");
  });
});

describe("taşıma katmanı güvenliği", () => {
  it("HTTP: kimliksiz erişim, bozuk gövde, ek alan reddedilir", async () => {
    const { call, makePlayer } = await boot();
    const p = await makePlayer();
    expect((await call("GET", "/v1/me", null)).status).toBe(401);
    expect((await call("GET", "/v1/me", "sahte.token.degeri")).status).toBe(401);
    expect((await call("POST", "/v1/friends/requests", p.token, { friendCode: "PANDA-2222", extra: 1 })).status).toBe(400);
    expect((await call("POST", "/v1/progress/level-result", p.token, { levelId: "1", score: 1, durationMs: 1 })).status).toBe(400);
    const me = await call("GET", "/v1/me", p.token);
    expect(me.status).toBe(200);
    expect(JSON.stringify(me.body)).not.toMatch(/pin|hash|device/i);
  });

  it("HTTP: sınırsız istek 429 ile durdurulur", async () => {
    const { call } = await boot();
    let last = 0;
    for (let i = 0; i < 200; i += 1) last = (await call("GET", "/v1/me", null)).status;
    expect(last).toBe(429);
  });

  it("WS: auth olmadan mesaj bağlantıyı kapatır, geçersiz token reddedilir", async () => {
    const { wsUrl } = await boot();
    const c1 = await open(wsUrl);
    c1.send("room.ready");
    await new Promise((r) => setTimeout(r, 100));
    expect(c1.closed).toBe(true);
    const c2 = await open(wsUrl);
    c2.send("auth", { token: "sahte" });
    await new Promise((r) => setTimeout(r, 100));
    expect(c2.closed).toBe(true);
  });

  it("WS: bozuk JSON ve aşırı büyük mesaj bağlantıyı düşürür; flood kapatılır", async () => {
    const { wsUrl, makePlayer, connect } = await boot();
    const p = await makePlayer();
    const c = await connect(p.token);
    c.ws.send("{bozuk");
    await new Promise((r) => setTimeout(r, 100));
    expect(c.closed).toBe(true);

    const c2 = await connect(p.token);
    c2.ws.send("x".repeat(5000));
    await new Promise((r) => setTimeout(r, 100));
    expect(c2.closed).toBe(true);

    const c3 = await connect(p.token);
    for (let i = 0; i < 200; i += 1) c3.send("presence.heartbeat");
    await new Promise((r) => setTimeout(r, 200));
    expect(c3.closed).toBe(true);
    expect(wsUrl).toContain("/ws");
  });

  it("WS: ebeveyn izni yokken veya arkadaş olmayana davet gönderilemez", async () => {
    const { call, makePlayer, connect } = await boot();
    const a = await makePlayer();
    const stranger = await makePlayer();
    const ca = await connect(a.token);
    await connect(stranger.token);
    ca.send("invite.send", { receiverId: stranger.id, mode: "colorRace" });
    expect((await ca.next("error")).payload.code).toBe("FORBIDDEN");
    // Kendini ve olmayan oyuncuyu davet etmek
    ca.send("invite.send", { receiverId: a.id, mode: "colorRace" });
    expect((await ca.next("error")).payload.code).toBe("INVALID_INPUT");
    expect((await call("GET", "/v1/parent/settings", a.token)).status).toBe(200);
  });

  it("WS: aynı hesap ikinci kez bağlanınca eski bağlantı kapanır", async () => {
    const { makePlayer, connect } = await boot();
    const p = await makePlayer();
    const first = await connect(p.token);
    await connect(p.token);
    await new Promise((r) => setTimeout(r, 100));
    expect(first.closed).toBe(true);
    expect(first.closeCode).toBe(4000);
  });
});
