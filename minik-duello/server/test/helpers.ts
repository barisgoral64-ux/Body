import { loadConfig } from "../src/config/env.js";
import { createContainer, type Container } from "../src/container.js";
import type { RandomInt } from "../src/domain/identity.js";
import { randomBytes } from "node:crypto";
import { Pool } from "pg";
import { afterEach } from "vitest";
import { createLogger } from "../src/infra/logger.js";
import { MemoryStore } from "../src/infra/memoryStore.js";
import { runMigrations, seedStaticData } from "../src/infra/migrate.js";
import { PgStore } from "../src/infra/pgStore.js";
import { FakeTime } from "../src/infra/runtime.js";
import type { DataStore } from "../src/infra/store.js";
import type { RoundView, ServerMessage } from "../src/protocol.js";

export const PIN = "1234";

export function seededRandom(seed: number): RandomInt {
  let a = seed >>> 0;
  return (max) => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return Math.floor((((t ^ (t >>> 14)) >>> 0) / 4294967296) * max);
  };
}

/** TEST_DATABASE_URL verilirse testler gerçek PostgreSQL'de (test başına ayrı şema) koşar. */
const PG_URL = process.env.TEST_DATABASE_URL;
const cleanups: (() => Promise<void>)[] = [];
afterEach(async () => {
  for (const fn of cleanups.splice(0)) await fn();
});

export interface TestStore {
  readonly store: DataStore;
  readonly matchCount: () => Promise<number>;
}

export async function createTestStore(): Promise<TestStore> {
  if (!PG_URL) {
    const memory = new MemoryStore();
    return { store: memory, matchCount: () => Promise.resolve(memory.savedMatches().length) };
  }
  const schema = `t_${randomBytes(6).toString("hex")}`;
  const admin = new Pool({ connectionString: PG_URL, max: 1 });
  await admin.query(`CREATE SCHEMA ${schema}`);
  const pool = new Pool({ connectionString: PG_URL, options: `-c search_path=${schema}`, max: 6 });
  await runMigrations(pool);
  await seedStaticData(pool);
  const lockPool = new Pool({ connectionString: PG_URL, max: 4 });
  cleanups.push(async () => {
    await pool.end();
    await lockPool.end();
    await admin.query(`DROP SCHEMA ${schema} CASCADE`);
    await admin.end();
  });
  return {
    store: new PgStore(pool, lockPool),
    matchCount: async () => Number((await pool.query("SELECT COUNT(*) AS n FROM matches")).rows[0].n),
  };
}

export interface TestEnv {
  readonly time: FakeTime;
  readonly store: DataStore;
  readonly matchCount: () => Promise<number>;
  readonly c: Container;
  /** Sunucudan oyunculara giden tüm mesajlar. */
  readonly sent: { to: string; message: ServerMessage }[];
}

export async function createEnv(seed = 1): Promise<TestEnv> {
  const time = new FakeTime();
  const { store, matchCount } = await createTestStore();
  const logger = createLogger("error", "test", () => undefined);
  const c = createContainer(loadConfig({}), logger, { store, clock: time, scheduler: time, random: seededRandom(seed) });
  const sent: { to: string; message: ServerMessage }[] = [];
  c.bindSink({ deliver: (to, message) => void sent.push({ to, message }), onRoomClosed: () => undefined });
  return { time, store, matchCount, c, sent };
}

let deviceCounter = 0;

export interface TestPlayer {
  readonly id: string;
  readonly code: string;
  readonly accessToken: string;
}

export async function newPlayer(env: TestEnv): Promise<TestPlayer> {
  deviceCounter += 1;
  const r = await env.c.auth.registerAnonymous(`test-device-${String(deviceCounter).padStart(8, "0")}-abcdefgh`);
  if (!r.ok) throw new Error(`kayıt başarısız: ${r.error.code}`);
  return { id: r.value.player.playerId, code: r.value.player.friendCode, accessToken: r.value.tokens.accessToken };
}

export async function enableSocial(env: TestEnv, id: string): Promise<void> {
  const pin = await env.c.parent.setPin(id, PIN);
  if (!pin.ok) throw new Error("PIN");
  const r = await env.c.parent.updateSettings(
    id,
    { friendsEnabled: true, multiplayerEnabled: true, onlineStatusVisible: true, gameInvitationsEnabled: true },
    PIN,
  );
  if (!r.ok) throw new Error(`ayar: ${r.error.code}`);
}

export async function makeFriends(env: TestEnv, a: TestPlayer, b: TestPlayer): Promise<void> {
  await enableSocial(env, a.id);
  await enableSocial(env, b.id);
  const sent = await env.c.friendRequests.send(a.id, b.code);
  if (!sent.ok) throw new Error(`istek: ${sent.error.code}`);
  const incoming = await env.c.friendRequests.listIncoming(b.id);
  if (!incoming.ok || !incoming.value[0]) throw new Error("istek yok");
  const resp = await env.c.friendRequests.respond(b.id, incoming.value[0].requestId, true);
  if (!resp.ok) throw new Error("kabul");
}

export const messagesTo = (env: TestEnv, to: string, type: string): ServerMessage[] =>
  env.sent.filter((s) => s.to === to && s.message.type === type).map((s) => s.message);

export function correctChoices(view: RoundView): string[] {
  const p = view.params;
  switch (view.kind) {
    case "color":
    case "shape":
      return [String(p.target)];
    case "number":
      return [String(p.count)];
    case "memory":
      return [String(p.sequence).split(",")[Number(p.position) - 1] as string];
    case "puzzle":
      return [`${String(p.image)}_${Number(p.missing) - 1}`];
    case "stars":
      return view.choices.filter((c) => c.glyph === "star").map((c) => c.id);
  }
}

export const wrongChoice = (view: RoundView): string => {
  const right = new Set(correctChoices(view));
  return (view.choices.find((c) => !right.has(c.id)) as { id: string }).id;
};

