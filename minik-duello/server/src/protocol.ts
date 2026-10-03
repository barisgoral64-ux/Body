import { z } from "zod";
import { LIMITS, PROTOCOL_VERSION } from "./config/constants.js";
import { QUICK_CHAT_IDS } from "./domain/quickChat.js";

export const GAME_MODES = [
  "colorRace", "shapeRace", "numberRace", "memoryDuel", "puzzleRace", "starCollect",
  "coopStars", "coopPuzzle", "mixedMatch",
] as const;

export type RoundKind = "color" | "shape" | "number" | "memory" | "puzzle" | "stars";

export interface RoundChoice {
  readonly id: string;
  readonly glyph: string;
}

export interface RoundView {
  readonly roundId: string;
  readonly roundIndex: number;
  readonly totalRounds: number;
  readonly kind: RoundKind;
  readonly promptKey: string;
  readonly params: Readonly<Record<string, string | number>>;
  readonly choices: readonly RoundChoice[];
  /** true: turda birden çok doğru nesne bulunabilir (yıldız toplama). */
  readonly multi: boolean;
  readonly showMs: number;
  readonly durationMs: number;
}

export type ServerMessageType =
  | "room.state" | "room.ready" | "room.countdown" | "room.round" | "room.answerAck" | "room.roundResult"
  | "room.finished" | "room.opponentDisconnected" | "room.opponentReconnected" | "room.quickChat"
  | "invite.received" | "invite.resolved" | "presence.update" | "friend.event" | "auth.ok" | "error";

export interface ServerMessage {
  readonly v: typeof PROTOCOL_VERSION;
  readonly type: ServerMessageType;
  readonly payload: Readonly<Record<string, unknown>>;
}

export const serverMessage = (type: ServerMessageType, payload: Record<string, unknown> = {}): ServerMessage => ({
  v: PROTOCOL_VERSION,
  type,
  payload,
});

const MAX_ID_LENGTH = 64;
const idString = z.string().min(1).max(MAX_ID_LENGTH);

/** İstemciden gelen her mesaj bu şemadan geçer; bilinmeyen alan ve tip reddedilir. */
export const clientMessageSchema = z.discriminatedUnion("type", [
  z.object({ v: z.literal(PROTOCOL_VERSION), type: z.literal("auth"), payload: z.object({ token: z.string().min(1).max(LIMITS.wsMaxMessageBytes) }).strict() }).strict(),
  z.object({ v: z.literal(PROTOCOL_VERSION), type: z.literal("presence.heartbeat"), payload: z.object({}).strict().optional() }).strict(),
  z.object({ v: z.literal(PROTOCOL_VERSION), type: z.literal("invite.send"), payload: z.object({ receiverId: idString, mode: z.enum(GAME_MODES) }).strict() }).strict(),
  z.object({ v: z.literal(PROTOCOL_VERSION), type: z.literal("invite.respond"), payload: z.object({ inviteId: idString, accept: z.boolean() }).strict() }).strict(),
  z.object({ v: z.literal(PROTOCOL_VERSION), type: z.literal("room.ready"), payload: z.object({}).strict().optional() }).strict(),
  z.object({ v: z.literal(PROTOCOL_VERSION), type: z.literal("room.answer"), payload: z.object({ roundId: idString, choiceId: idString }).strict() }).strict(),
  z.object({ v: z.literal(PROTOCOL_VERSION), type: z.literal("room.quickChat"), payload: z.object({ message: z.enum(QUICK_CHAT_IDS) }).strict() }).strict(),
  z.object({ v: z.literal(PROTOCOL_VERSION), type: z.literal("room.leave"), payload: z.object({}).strict().optional() }).strict(),
  z.object({ v: z.literal(PROTOCOL_VERSION), type: z.literal("room.resume"), payload: z.object({ roomId: idString, resumeToken: idString }).strict() }).strict(),
]);

export type ClientMessage = z.infer<typeof clientMessageSchema>;
