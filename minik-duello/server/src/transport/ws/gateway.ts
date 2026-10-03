import type { IncomingMessage, Server as HttpServer } from "node:http";
import { WebSocketServer, type WebSocket } from "ws";
import { AUTH, LIMITS } from "../../config/constants.js";
import type { Container, PushSink } from "../../container.js";
import type { PlayerId } from "../../domain/models.js";
import { maskPlayerId } from "../../infra/logger.js";
import { RateLimiter } from "../../infra/rateLimiter.js";
import { clientMessageSchema, serverMessage, type ClientMessage, type ServerMessage } from "../../protocol.js";
import { ErrorCode, type AppError, type Result } from "../../shared/result.js";

const WS_PATH = "/ws";
const MS_PER_SECOND = 1000;
const FLOOD_MULTIPLIER = 3;
const CLOSE_AUTH_TIMEOUT = 4401;
const CLOSE_REPLACED = 4000;
const CLOSE_PROTOCOL = 4400;
const CLOSE_FLOOD = 4429;
const CLOSE_ACCOUNT_DELETED = 4001;

interface Connection {
  readonly socket: WebSocket;
  readonly ip: string;
  playerId: PlayerId | null;
  readonly authTimer: ReturnType<typeof setTimeout>;
  windowStart: number;
  windowCount: number;
}

/**
 * WebSocket geçidi. İstemci yalnızca girdi gönderir; tüm kararlar servislerde (sunucu otoriter).
 * İlk mesaj `auth` olmak zorundadır; token URL'de taşınmaz.
 */
export class Gateway implements PushSink {
  private readonly wss: WebSocketServer;
  private readonly connections = new Map<WebSocket, Connection>();
  private readonly byPlayer = new Map<PlayerId, Connection>();
  private readonly ipCounts = new Map<string, number>();
  private readonly log;

  constructor(private readonly c: Container) {
    this.log = c.logger.child("ws");
    this.wss = new WebSocketServer({ noServer: true, maxPayload: LIMITS.wsMaxMessageBytes });
    c.bindSink(this);
  }

  attach(server: HttpServer): void {
    server.on("upgrade", (request, socket, head) => {
      if (request.url?.split("?")[0] !== WS_PATH) {
        socket.destroy();
        return;
      }
      // Aynı IP'den çok sayıda eşzamanlı bağlantı (kaynak tüketme) reddedilir.
      const ip = clientIp(request, this.c.config.trustProxy);
      if ((this.ipCounts.get(ip) ?? 0) >= LIMITS.wsConnectionsPerIp) {
        socket.write("HTTP/1.1 429 Too Many Requests\r\nConnection: close\r\n\r\n");
        socket.destroy();
        return;
      }
      this.wss.handleUpgrade(request, socket, head, (ws) => this.onConnection(ws, ip));
    });
  }

  close(): void {
    for (const conn of this.connections.values()) {
      clearTimeout(conn.authTimer);
      conn.socket.close();
    }
    this.wss.close();
  }

  // --- PushSink ---
  deliver(to: PlayerId, message: ServerMessage): void {
    const conn = this.byPlayer.get(to);
    if (conn && conn.socket.readyState === conn.socket.OPEN) conn.socket.send(JSON.stringify(message));
  }

  onRoomClosed(players: readonly PlayerId[]): void {
    for (const p of players) {
      if (this.byPlayer.has(p)) {
        this.c.presence.set(p, "online");
        this.broadcastPresence(p);
      }
    }
  }

  disconnect(playerId: PlayerId): void {
    const conn = this.byPlayer.get(playerId);
    if (!conn) return;
    this.byPlayer.delete(playerId);
    this.c.presence.clear(playerId);
    conn.socket.close(CLOSE_ACCOUNT_DELETED, "account deleted");
  }

  isOnline(playerId: PlayerId): boolean {
    return this.byPlayer.has(playerId);
  }

  // --- Bağlantı yaşam döngüsü ---
  private onConnection(socket: WebSocket, ip: string): void {
    this.ipCounts.set(ip, (this.ipCounts.get(ip) ?? 0) + 1);
    const conn: Connection = {
      socket,
      ip,
      playerId: null,
      windowStart: this.c.clock.now().getTime(),
      windowCount: 0,
      authTimer: setTimeout(() => socket.close(CLOSE_AUTH_TIMEOUT, "auth timeout"), AUTH.wsAuthTimeoutMs),
    };
    this.connections.set(socket, conn);

    socket.on("message", (data) => {
      void this.onMessage(conn, data.toString()).catch((error: unknown) => {
        this.log.error("message_failed", { message: error instanceof Error ? error.message : "unknown" });
        this.sendError(conn, ErrorCode.Internal);
      });
    });
    socket.on("close", () => this.onClose(conn));
    socket.on("error", () => socket.close());
  }

  private onClose(conn: Connection): void {
    clearTimeout(conn.authTimer);
    const remaining = (this.ipCounts.get(conn.ip) ?? 1) - 1;
    if (remaining <= 0) this.ipCounts.delete(conn.ip);
    else this.ipCounts.set(conn.ip, remaining);
    this.connections.delete(conn.socket);
    const playerId = conn.playerId;
    // Yeni bağlantı eskisinin yerini aldıysa oda/durum etkilenmez.
    if (!playerId || this.byPlayer.get(playerId) !== conn) return;
    this.byPlayer.delete(playerId);
    this.c.presence.clear(playerId);
    this.c.rooms.roomOf(playerId)?.disconnect(playerId);
    this.broadcastPresence(playerId);
    this.log.debug("disconnected", { player: maskPlayerId(playerId) });
  }

  private allow(conn: Connection): boolean {
    const now = this.c.clock.now().getTime();
    if (now - conn.windowStart >= MS_PER_SECOND) {
      conn.windowStart = now;
      conn.windowCount = 0;
    }
    conn.windowCount += 1;
    if (conn.windowCount > LIMITS.wsMessagesPerSecond * FLOOD_MULTIPLIER) {
      conn.socket.close(CLOSE_FLOOD, "flood");
      return false;
    }
    return conn.windowCount <= LIMITS.wsMessagesPerSecond;
  }

  private async onMessage(conn: Connection, raw: string): Promise<void> {
    if (!this.allow(conn)) {
      this.sendError(conn, ErrorCode.RateLimited);
      return;
    }
    let json: unknown;
    try {
      json = JSON.parse(raw);
    } catch {
      conn.socket.close(CLOSE_PROTOCOL, "bad json");
      return;
    }
    const parsed = clientMessageSchema.safeParse(json);
    if (!parsed.success) {
      this.sendError(conn, ErrorCode.InvalidInput);
      return;
    }
    const message = parsed.data;

    if (!conn.playerId) {
      if (message.type !== "auth") {
        conn.socket.close(CLOSE_PROTOCOL, "auth required");
        return;
      }
      await this.authenticate(conn, message.payload.token);
      return;
    }
    if (message.type === "auth") return; // Tekrar auth yok sayılır.
    await this.dispatch(conn, conn.playerId, message);
  }

  private async authenticate(conn: Connection, token: string): Promise<void> {
    const verified = this.c.auth.verifyAccessToken(token);
    if (!verified.ok) {
      conn.socket.close(CLOSE_AUTH_TIMEOUT, "unauthorized");
      return;
    }
    clearTimeout(conn.authTimer);
    const playerId = verified.value;
    const previous = this.byPlayer.get(playerId);
    conn.playerId = playerId;
    this.byPlayer.set(playerId, conn);
    if (previous && previous !== conn) previous.socket.close(CLOSE_REPLACED, "replaced");

    const room = this.c.rooms.roomOf(playerId);
    this.c.presence.set(playerId, room?.state === "playing" ? "playing" : "online");
    this.deliver(playerId, serverMessage("auth.ok", { playerId, roomId: room?.roomId ?? null }));
    this.broadcastPresence(playerId);
  }

  private async dispatch(conn: Connection, playerId: PlayerId, message: Exclude<ClientMessage, { type: "auth" }>): Promise<void> {
    switch (message.type) {
      case "presence.heartbeat":
        this.c.presence.heartbeat(playerId);
        return;

      case "invite.send": {
        const result = await this.c.multiplayer.sendInvite(playerId, message.payload.receiverId, message.payload.mode);
        this.reply(conn, result, (invite) => serverMessage("invite.resolved", { inviteId: invite.inviteId, status: "sent" }));
        return;
      }
      case "invite.respond": {
        const result = await this.c.multiplayer.respondToInvite(playerId, message.payload.inviteId, message.payload.accept);
        if (!result.ok) {
          this.sendError(conn, result.error.code);
          return;
        }
        if (result.value) {
          for (const p of result.value.players) this.c.presence.set(p, "playing");
          for (const p of result.value.players) this.broadcastPresence(p);
        }
        return;
      }

      case "room.ready":
        this.reply(conn, this.c.rooms.roomOf(playerId)?.setReady(playerId) ?? noRoom());
        return;
      case "room.answer":
        this.reply(conn, this.c.rooms.roomOf(playerId)?.answer(playerId, message.payload.roundId, message.payload.choiceId) ?? noRoom());
        return;
      case "room.quickChat":
        this.reply(conn, this.c.rooms.roomOf(playerId)?.sendQuickChat(playerId, message.payload.message) ?? noRoom());
        return;
      case "room.leave":
        this.c.rooms.roomOf(playerId)?.leave(playerId);
        return;
      case "room.resume": {
        const room = this.c.rooms.get(message.payload.roomId);
        this.reply(conn, room?.resume(playerId, message.payload.resumeToken) ?? noRoom());
        if (room?.hasPlayer(playerId)) {
          this.c.presence.set(playerId, "playing");
          this.broadcastPresence(playerId);
        }
        return;
      }
    }
  }

  private reply<T>(conn: Connection, result: Result<T>, onOk?: (value: T) => ServerMessage): void {
    if (!result.ok) {
      this.sendError(conn, result.error.code);
      return;
    }
    if (onOk && conn.socket.readyState === conn.socket.OPEN) conn.socket.send(JSON.stringify(onOk(result.value)));
  }

  private sendError(conn: Connection, code: ErrorCode): void {
    if (conn.socket.readyState === conn.socket.OPEN) conn.socket.send(JSON.stringify(serverMessage("error", { code })));
  }

  /** Durum değişimini, gizlilik kurallarını uygulayarak çevrimiçi arkadaşlara iletir. */
  private broadcastPresence(playerId: PlayerId): void {
    void (async () => {
      try {
        for (const friendId of await this.c.store.listFriendIds(playerId)) {
          if (!this.byPlayer.has(friendId)) continue;
          const status = await this.c.presence.visibleTo(friendId, playerId);
          this.deliver(friendId, serverMessage("presence.update", { playerId, status }));
        }
      } catch (error) {
        this.log.warn("presence_broadcast_failed", { message: error instanceof Error ? error.message : "unknown" });
      }
    })();
  }
}

function noRoom(): Result<never> {
  const error: AppError = { code: ErrorCode.InvalidState, message: "Odada değilsin" };
  return { ok: false, error };
}

/** İstemci IP'si. Yalnızca güvenilen yük dengeleyici arkasında X-Forwarded-For'un İLK değeri kullanılır. */
export function clientIp(request: IncomingMessage, trustProxy: boolean): string {
  if (trustProxy) {
    const header = request.headers["x-forwarded-for"];
    const first = (Array.isArray(header) ? header[0] : header)?.split(",")[0]?.trim();
    if (first) return first;
  }
  return request.socket.remoteAddress ?? "unknown";
}
