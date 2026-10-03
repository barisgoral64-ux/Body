import type { FastifyInstance, FastifyReply, FastifyRequest } from "fastify";
import { z } from "zod";
import { AUTH, LIMITS, PARENT } from "../../config/constants.js";
import type { Container } from "../../container.js";
import { compareVersions } from "../../domain/version.js";
import { RateLimiter } from "../../infra/rateLimiter.js";
import { ErrorCode, type Result } from "../../shared/result.js";

declare module "fastify" {
  interface FastifyRequest {
    playerId: string;
  }
}

const STATUS_BY_CODE: Record<ErrorCode, number> = {
  [ErrorCode.InvalidInput]: 400,
  [ErrorCode.Unauthorized]: 401,
  [ErrorCode.Forbidden]: 403,
  [ErrorCode.NotFound]: 404,
  [ErrorCode.RateLimited]: 429,
  [ErrorCode.FriendsDisabled]: 403,
  [ErrorCode.MultiplayerDisabled]: 403,
  [ErrorCode.InvitesDisabled]: 403,
  [ErrorCode.Blocked]: 403,
  [ErrorCode.AlreadyFriends]: 409,
  [ErrorCode.RequestPending]: 409,
  [ErrorCode.RequestExpired]: 410,
  [ErrorCode.FriendLimitReached]: 409,
  [ErrorCode.InviteExpired]: 410,
  [ErrorCode.OpponentUnavailable]: 409,
  [ErrorCode.InvalidState]: 409,
  [ErrorCode.ProtocolMismatch]: 426,
  [ErrorCode.Internal]: 500,
};

const MS_PER_MINUTE = 60_000;
const pinSchema = z.string().regex(new RegExp(`^\\d{${PARENT.pinLength}}$`));
const idSchema = z.string().min(1).max(64);

/** Sonucu HTTP yanıtına çevirir. Yalnızca hata KODU döner; ayrıntı mesajı istemciye sızdırılmaz. */
function respond<T>(reply: FastifyReply, result: Result<T>, map: (v: T) => unknown = (v) => v): unknown {
  if (result.ok) return map(result.value);
  return reply.status(STATUS_BY_CODE[result.error.code]).send({ code: result.error.code });
}

function parse<S extends z.ZodTypeAny>(schema: S, data: unknown, reply: FastifyReply): z.infer<S> | null {
  const parsed = schema.safeParse(data);
  if (parsed.success) return parsed.data as z.infer<S>;
  void reply.status(400).send({ code: ErrorCode.InvalidInput });
  return null;
}

export function registerRoutes(app: FastifyInstance, c: Container): void {
  const limiter = new RateLimiter(LIMITS.httpRequestsPerMinute, MS_PER_MINUTE, c.clock);
  const authLimiter = new RateLimiter(LIMITS.httpRequestsPerMinute / 4, MS_PER_MINUTE, c.clock);

  app.decorateRequest("playerId", "");

  app.addHook("onRequest", (request, reply, done) => {
    // Çok eski istemci (X-App-Version asgari sürümün altında) zorunlu güncellemeye yönlendirilir.
    const sent = request.headers["x-app-version"];
    const path = request.url.split("?")[0] ?? "";
    if (typeof sent === "string" && path.startsWith("/v1") && path !== "/v1/app-version" && compareVersions(sent, c.config.appMinVersion) === -1) {
      void reply.status(426).send({ code: ErrorCode.ProtocolMismatch });
      return;
    }
    const key = request.ip;
    const bucket = request.url.startsWith("/v1/auth") ? authLimiter : limiter;
    if (!bucket.hit(key)) {
      void reply.status(429).send({ code: ErrorCode.RateLimited });
      return;
    }
    done();
  });

  const requireAuth = async (request: FastifyRequest, reply: FastifyReply): Promise<void> => {
    const header = request.headers.authorization ?? "";
    const token = header.startsWith("Bearer ") ? header.slice("Bearer ".length) : "";
    const verified = c.auth.verifyAccessToken(token);
    if (!verified.ok) {
      await reply.status(401).send({ code: ErrorCode.Unauthorized });
      return;
    }
    request.playerId = verified.value;
  };
  const secured = { preHandler: requireAuth };

  // --- Sürüm / güncelleme (herkese açık, kimlik gerektirmez) ---
  app.get("/v1/app-version", () => ({
    minSupportedVersion: c.config.appMinVersion,
    latestVersion: c.config.appLatestVersion,
    updateUrl: c.config.appUpdateUrl,
  }));

  // --- Kimlik ---
  app.post("/v1/auth/anonymous", async (req, reply) => {
    const body = parse(z.object({ deviceId: z.string().min(AUTH.deviceIdMinLength).max(AUTH.deviceIdMaxLength) }).strict(), req.body, reply);
    if (!body) return reply;
    const result = await c.auth.registerAnonymous(body.deviceId);
    return respond(reply, result, (v) => ({
      tokens: v.tokens, isNew: v.isNew,
      player: { playerId: v.player.playerId, username: v.player.username, friendCode: v.player.friendCode },
    }));
  });

  app.post("/v1/auth/refresh", async (req, reply) => {
    const body = parse(z.object({ refreshToken: z.string().min(1).max(2048) }).strict(), req.body, reply);
    if (!body) return reply;
    return respond(reply, await c.auth.refresh(body.refreshToken));
  });

  // --- Oyuncu ---
  app.get("/v1/me", secured, async (req, reply) => respond(reply, await c.players.getSelf(req.playerId)));

  // --- Bölümler ve ilerleme ---
  app.get("/v1/levels", secured, () => ({ levels: c.levels.getCatalog() }));
  app.get("/v1/progress", secured, async (req) => c.levels.getProgress(req.playerId));
  app.post("/v1/progress/level-result", secured, async (req, reply) => {
    const body = parse(z.object({ levelId: z.number().int(), score: z.number().int(), durationMs: z.number() }).strict(), req.body, reply);
    if (!body) return reply;
    return respond(reply, await c.levels.submitResult(req.playerId, body.levelId, body.score, body.durationMs));
  });

  // --- Ödüller ---
  app.get("/v1/rewards/inventory", secured, async (req) => ({ items: await c.rewards.listInventory(req.playerId) }));
  app.get("/v1/rewards/daily", secured, async (req) => c.rewards.getDailyStatus(req.playerId));
  app.post("/v1/rewards/daily/claim", secured, async (req, reply) => respond(reply, await c.rewards.claimDaily(req.playerId)));
  app.post("/v1/rewards/buy", secured, async (req, reply) => {
    const body = parse(z.object({ rewardId: idSchema }).strict(), req.body, reply);
    if (!body) return reply;
    return respond(reply, await c.rewards.buy(req.playerId, body.rewardId));
  });
  app.post("/v1/rewards/equip", secured, async (req, reply) => {
    const body = parse(z.object({ rewardId: idSchema, equipped: z.boolean() }).strict(), req.body, reply);
    if (!body) return reply;
    return respond(reply, await c.rewards.equip(req.playerId, body.rewardId, body.equipped));
  });

  // --- Arkadaşlar ---
  app.get("/v1/friends", secured, async (req, reply) => respond(reply, await c.friends.listFriends(req.playerId), (friends) => ({ friends })));
  app.post("/v1/friends/requests", secured, async (req, reply) => {
    const body = parse(z.object({ friendCode: z.string().min(1).max(32) }).strict(), req.body, reply);
    if (!body) return reply;
    return respond(reply, await c.friendRequests.send(req.playerId, body.friendCode), () => ({ ok: true }));
  });
  app.get("/v1/friends/requests/incoming", secured, async (req, reply) =>
    respond(reply, await c.friendRequests.listIncoming(req.playerId), (requests) => ({ requests })));
  app.post("/v1/friends/requests/:id/respond", secured, async (req, reply) => {
    const params = parse(z.object({ id: idSchema }), req.params, reply);
    const body = parse(z.object({ accept: z.boolean() }).strict(), req.body, reply);
    if (!params || !body) return reply;
    return respond(reply, await c.friendRequests.respond(req.playerId, params.id, body.accept), () => ({ ok: true }));
  });
  app.get("/v1/leaderboard/weekly", secured, async (req, reply) =>
    respond(reply, await c.leaderboard.weeklyAmongFriends(req.playerId), (entries) => ({ entries })));

  // --- Bulut kaydı ---
  app.get("/v1/save", secured, async (req, reply) => respond(reply, await c.saves.load(req.playerId), (save) => ({ save })));
  app.put("/v1/save", secured, async (req, reply) => {
    const body = parse(z.object({ data: z.record(z.unknown()) }).strict(), req.body, reply);
    if (!body) return reply;
    return respond(reply, await c.saves.save(req.playerId, body.data));
  });

  // --- Ebeveyn paneli ---
  app.get("/v1/parent/settings", secured, async (req, reply) => respond(reply, await c.parent.getSettings(req.playerId)));
  app.put("/v1/parent/pin", secured, async (req, reply) => {
    const body = parse(z.object({ newPin: pinSchema, currentPin: pinSchema.optional() }).strict(), req.body, reply);
    if (!body) return reply;
    return respond(reply, await c.parent.setPin(req.playerId, body.newPin, body.currentPin), () => ({ ok: true }));
  });
  app.put("/v1/parent/settings", secured, async (req, reply) => {
    const body = parse(
      z.object({
        pin: pinSchema.optional(),
        patch: z.object({
          friendsEnabled: z.boolean().optional(), multiplayerEnabled: z.boolean().optional(),
          onlineStatusVisible: z.boolean().optional(), gameInvitationsEnabled: z.boolean().optional(),
          dailyLimitMinutes: z.number().int().optional(), soundEnabled: z.boolean().optional(),
        }).strict(),
      }).strict(),
      req.body, reply,
    );
    if (!body) return reply;
    return respond(reply, await c.parent.updateSettings(req.playerId, body.patch, body.pin));
  });
  app.post("/v1/parent/block", secured, async (req, reply) => {
    const body = parse(z.object({ pin: pinSchema, blockedId: idSchema }).strict(), req.body, reply);
    if (!body) return reply;
    return respond(reply, await c.parent.blockPlayer(req.playerId, body.blockedId, body.pin), () => ({ ok: true }));
  });
  app.post("/v1/parent/unblock", secured, async (req, reply) => {
    const body = parse(z.object({ pin: pinSchema, blockedId: idSchema }).strict(), req.body, reply);
    if (!body) return reply;
    return respond(reply, await c.parent.unblockPlayer(req.playerId, body.blockedId, body.pin), () => ({ ok: true }));
  });
  app.post("/v1/parent/friends/remove", secured, async (req, reply) => {
    const body = parse(z.object({ pin: pinSchema, friendId: idSchema }).strict(), req.body, reply);
    if (!body) return reply;
    return respond(reply, await c.parent.removeFriend(req.playerId, body.friendId, body.pin), () => ({ ok: true }));
  });
  app.post("/v1/parent/delete-account", secured, async (req, reply) => {
    const body = parse(z.object({ pin: pinSchema, confirm: z.literal(true) }).strict(), req.body, reply);
    if (!body) return reply;
    return respond(reply, await c.parent.deleteAccount(req.playerId, body.pin), () => ({ ok: true }));
  });
  app.post("/v1/parent/regenerate-code", secured, async (req, reply) => {
    const body = parse(z.object({ pin: pinSchema }).strict(), req.body, reply);
    if (!body) return reply;
    const check = await c.parent.verifyPin(req.playerId, body.pin);
    if (!check.ok) return respond(reply, check);
    return respond(reply, await c.players.regenerateFriendCode(req.playerId), (friendCode) => ({ friendCode }));
  });
}
