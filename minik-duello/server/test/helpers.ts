import { loadConfig } from "../src/config/env.js";
import { createContainer, type Container } from "../src/container.js";
import type { RandomInt } from "../src/domain/identity.js";
import { createLogger } from "../src/infra/logger.js";
import { MemoryStore } from "../src/infra/memoryStore.js";
import { FakeTime } from "../src/infra/runtime.js";
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

export interface TestEnv {
  readonly time: FakeTime;
  readonly store: MemoryStore;
  readonly c: Container;
  /** Sunucudan oyunculara giden tüm mesajlar. */
  readonly sent: { to: string; message: ServerMessage }[];
}

export function createEnv(seed = 1): TestEnv {
  const time = new FakeTime();
  const store = new MemoryStore();
  const logger = createLogger("error", "test", () => undefined);
  const c = createContainer(loadConfig({}), logger, { store, clock: time, scheduler: time, random: seededRandom(seed) });
  const sent: { to: string; message: ServerMessage }[] = [];
  c.bindSink({ deliver: (to, message) => void sent.push({ to, message }), onRoomClosed: () => undefined });
  return { time, store, c, sent };
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

