import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import { ROOM, ROUND_TIMING } from "../src/config/constants.js";
import { clientMessageSchema, type ServerMessage } from "../src/protocol.js";
import { correctChoices, createEnv, enableSocial, messagesTo, newPlayer, wrongChoice } from "./helpers.js";
import type { RoundView } from "../src/protocol.js";

const DIR = join(dirname(fileURLToPath(import.meta.url)), "..", "..", "protocol");
const CLIENT_FILE = join(DIR, "client-messages.json");
const SERVER_FILE = join(DIR, "server-messages.json");
const UPDATE = process.env.UPDATE_FIXTURES === "1";

const clientMessages: Record<string, unknown> = {
  auth: { v: 1, type: "auth", payload: { token: "TOKEN" } },
  heartbeat: { v: 1, type: "presence.heartbeat" },
  inviteSend: { v: 1, type: "invite.send", payload: { receiverId: "f1", mode: "mixedMatch" } },
  inviteRespond: { v: 1, type: "invite.respond", payload: { inviteId: "i1", accept: true } },
  ready: { v: 1, type: "room.ready" },
  answer: { v: 1, type: "room.answer", payload: { roundId: "r", choiceId: "c" } },
  quickChat: { v: 1, type: "room.quickChat", payload: { message: "hello" } },
  leave: { v: 1, type: "room.leave" },
  resume: { v: 1, type: "room.resume", payload: { roomId: "room", resumeToken: "tok" } },
};

/** Bir değerin yalnızca ŞEKLİNİ (anahtarlar + tipler) döndürür: sürüm farklarını değil uyumsuzluğu yakalar. */
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;

function shape(v: unknown): unknown {
  if (Array.isArray(v)) return v.length ? [shape(v[0])] : [];
  if (v && typeof v === "object") {
    // Oyuncu kimliğiyle anahtarlanan sözlüklerde anahtar adı değil, değer şekli önemlidir.
    return Object.fromEntries(
      Object.entries(v as Record<string, unknown>)
        .map(([k, x]) => [UUID.test(k) ? "<playerId>" : k, shape(x)] as const)
        .sort(([a], [b]) => a.localeCompare(b)),
    );
  }
  return v === null ? "null" : typeof v;
}

async function captureServerMessages(): Promise<Record<string, ServerMessage["payload"]>> {
  const env = await createEnv(3);
  const a = await newPlayer(env);
  const b = await newPlayer(env);
  await enableSocial(env, a.id);
  await enableSocial(env, b.id);
  const out: Record<string, ServerMessage["payload"]> = {};
  const grab = (to: string, type: string): void => {
    const m = messagesTo(env, to, type)[0];
    if (m && !out[type]) out[type] = m.payload;
  };

  const room = env.c.rooms.createRoom(a.id, b.id, "mixedMatch");
  if (!room.ok) throw new Error("oda");
  room.value.setReady(a.id);
  room.value.setReady(b.id);
  env.time.advance(ROOM.countdownSeconds * 1000);
  room.value.sendQuickChat(a.id, "hello");
  const rounds: RoundView[] = [];
  for (let i = 0; i < 5; i += 1) {
    const view = messagesTo(env, a.id, "room.round").at(-1)?.payload as unknown as RoundView;
    rounds.push(view);
    env.time.advance(view.showMs + 500);
    room.value.answer(a.id, view.roundId, correctChoices(view)[0] as string);
    room.value.answer(b.id, view.roundId, wrongChoice(view));
    if (i === 1) {
      room.value.disconnect(b.id);
      grab(a.id, "room.opponentDisconnected");
      const token = (messagesTo(env, b.id, "room.state")[0]?.payload as { resumeToken: string }).resumeToken;
      room.value.resume(b.id, token);
      grab(a.id, "room.opponentReconnected");
    }
    env.time.advance(ROUND_TIMING.betweenRoundsMs);
  }
  await room.value.persisted;
  for (const t of ["room.state", "room.countdown", "room.ready", "room.round", "room.answerAck", "room.roundResult", "room.finished"]) grab(a.id, t);
  grab(b.id, "room.quickChat");
  // Yıldız turu (çoklu seçim) şekli
  const env3 = await createEnv(5);
  const e = await newPlayer(env3);
  const f = await newPlayer(env3);
  const starRoom = env3.c.rooms.createRoom(e.id, f.id, "starCollect");
  if (!starRoom.ok) throw new Error("oda");
  starRoom.value.setReady(e.id);
  starRoom.value.setReady(f.id);
  env3.time.advance(ROOM.countdownSeconds * 1000);
  out["room.round.stars"] = messagesTo(env3, e.id, "room.round")[0]?.payload as ServerMessage["payload"];

  // Davet mesajları
  env.c.presence.set(b.id, "online");
  env.c.presence.set(a.id, "online");
  const env2 = await createEnv(4);
  const c = await newPlayer(env2);
  const d = await newPlayer(env2);
  const { makeFriends } = await import("./helpers.js");
  await makeFriends(env2, c, d);
  env2.c.presence.set(c.id, "online");
  env2.c.presence.set(d.id, "online");
  await env2.c.multiplayer.sendInvite(c.id, d.id, "colorRace");
  const inv = messagesTo(env2, d.id, "invite.received")[0];
  if (inv) out["invite.received"] = inv.payload;
  return out;
}

describe("protokol fixture'ları (istemci ↔ sunucu sözleşmesi)", () => {
  it("istemci mesaj örnekleri sunucu şemasından geçer", () => {
    for (const [name, msg] of Object.entries(clientMessages)) {
      expect(clientMessageSchema.safeParse(msg).success, name).toBe(true);
    }
    if (UPDATE) {
      mkdirSync(DIR, { recursive: true });
      writeFileSync(CLIENT_FILE, `${JSON.stringify(clientMessages, null, 2)}\n`);
    }
  });

  it("şemadan sapan istemci mesajları reddedilir", () => {
    const bad = [
      { v: 2, type: "room.ready" },
      { v: 1, type: "room.quickChat", payload: { message: "serbest metin" } },
      { v: 1, type: "room.answer", payload: { roundId: "r", choiceId: "c", extra: 1 } },
      { v: 1, type: "bilinmeyen" },
    ];
    for (const m of bad) expect(clientMessageSchema.safeParse(m).success).toBe(false);
  });

  it("sunucu mesajlarının şekli kayıtlı fixture ile aynı (sapma varsa istemci DTO'ları da güncellenmeli)", async () => {
    const captured = await captureServerMessages();
    if (UPDATE) {
      mkdirSync(DIR, { recursive: true });
      writeFileSync(SERVER_FILE, `${JSON.stringify(captured, null, 2)}\n`);
    }
    expect(existsSync(SERVER_FILE)).toBe(true);
    const saved = JSON.parse(readFileSync(SERVER_FILE, "utf8")) as Record<string, unknown>;
    expect(Object.keys(captured).sort()).toEqual(Object.keys(saved).sort());
    for (const key of Object.keys(saved)) {
      expect(shape(captured[key]), key).toEqual(shape(saved[key]));
    }
  });
});
