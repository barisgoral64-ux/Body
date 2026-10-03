import { FRIENDS } from "../config/constants.js";
import { createFriendRequest, respondToRequest } from "../domain/friendRequest.js";
import { normalizeFriendCode } from "../domain/identity.js";
import type { FriendRequest, PlayerId } from "../domain/models.js";
import type { Clock, IdGenerator } from "../infra/runtime.js";
import type { DataStore } from "../infra/store.js";
import { ErrorCode, fail, ok, type Result } from "../shared/result.js";
import type { PlayerService, PublicPlayerView } from "./playerService.js";

const MS_PER_DAY = 24 * 60 * 60 * 1000;

export interface IncomingRequestView {
  readonly requestId: string;
  readonly from: PublicPlayerView;
  readonly createdAt: Date;
  readonly expiresAt: Date;
}

export type FriendEvent = { type: "friend.requestReceived" | "friend.added"; to: PlayerId; from: PlayerId };

export class FriendRequestService {
  constructor(
    private readonly store: DataStore,
    private readonly players: PlayerService,
    private readonly clock: Clock,
    private readonly newId: IdGenerator,
    private readonly notify: (event: FriendEvent) => void = () => undefined,
  ) {}

  /**
   * Arkadaş koduyla istek gönderir. Alıcı tarafındaki durumlar (kod yok, engel, ebeveyn izni kapalı,
   * bekleyen istek, ret bekleme süresi) gönderene AYNI nötr başarı yanıtıyla döner: hesap varlığı sızdırılmaz.
   */
  async send(senderId: PlayerId, rawCode: string): Promise<Result<void>> {
    const senderSettings = await this.store.getSettings(senderId);
    if (!senderSettings?.friendsEnabled) return fail(ErrorCode.FriendsDisabled, "Arkadaş sistemi kapalı");

    const code = normalizeFriendCode(rawCode);
    if (!code) return fail(ErrorCode.InvalidInput, "Geçersiz arkadaş kodu");

    const now = this.clock.now();
    const dayAgo = new Date(now.getTime() - MS_PER_DAY);
    if ((await this.store.countCodeAttemptsSince(senderId, dayAgo)) >= FRIENDS.maxCodeAttemptsPerDay) {
      return fail(ErrorCode.RateLimited, "Günlük deneme sınırı");
    }
    await this.store.logCodeAttempt(senderId, now);
    if ((await this.store.countRequestsSentSince(senderId, dayAgo)) >= FRIENDS.maxRequestsPerDay) {
      return fail(ErrorCode.RateLimited, "Günlük istek sınırı");
    }

    const receiver = await this.store.getPlayerByFriendCode(code);
    if (receiver?.playerId === senderId) return fail(ErrorCode.InvalidInput, "Bu senin kodun");

    if (receiver && (await this.store.areFriends(senderId, receiver.playerId))) {
      return fail(ErrorCode.AlreadyFriends, "Zaten arkadaşsınız");
    }
    if ((await this.store.listFriendIds(senderId)).length >= FRIENDS.maxFriends) {
      return fail(ErrorCode.FriendLimitReached, "Arkadaş sınırı");
    }

    if (!receiver || !(await this.canReceive(senderId, receiver.playerId, now))) return ok(undefined);

    const created = createFriendRequest(this.newId(), senderId, receiver.playerId, now);
    if (!created.ok) return created;
    await this.store.insertFriendRequest(created.value);
    this.notify({ type: "friend.requestReceived", to: receiver.playerId, from: senderId });
    return ok(undefined);
  }

  async listIncoming(playerId: PlayerId): Promise<Result<readonly IncomingRequestView[]>> {
    const settings = await this.store.getSettings(playerId);
    if (!settings?.friendsEnabled) return fail(ErrorCode.FriendsDisabled, "Arkadaş sistemi kapalı");
    const pending = await this.store.listPendingIncoming(playerId, this.clock.now());
    const views: IncomingRequestView[] = [];
    for (const r of pending) {
      if (await this.store.isBlockedEitherWay(playerId, r.senderPlayerId)) continue;
      const from = await this.players.getPublic(r.senderPlayerId);
      if (from) views.push({ requestId: r.requestId, from, createdAt: r.createdAt, expiresAt: r.expiresAt });
    }
    return ok(views);
  }

  async respond(playerId: PlayerId, requestId: string, accept: boolean): Promise<Result<void>> {
    const settings = await this.store.getSettings(playerId);
    if (!settings?.friendsEnabled) return fail(ErrorCode.FriendsDisabled, "Arkadaş sistemi kapalı");

    const request = await this.store.getFriendRequest(requestId);
    // Başkasının isteğinin varlığı da sızdırılmaz.
    if (!request || request.receiverPlayerId !== playerId) return fail(ErrorCode.NotFound, "İstek bulunamadı");

    const now = this.clock.now();
    const outcome = respondToRequest(request, playerId, accept, now);
    if (!outcome.ok) {
      if (outcome.error.code === ErrorCode.RequestExpired) await this.store.setFriendRequestStatus(requestId, "expired", now);
      return outcome;
    }

    if (accept) {
      if (await this.store.isBlockedEitherWay(playerId, request.senderPlayerId)) {
        await this.store.setFriendRequestStatus(requestId, "rejected", now);
        return fail(ErrorCode.Blocked, "Engellendi");
      }
      const [mine, theirs] = await Promise.all([
        this.store.listFriendIds(playerId),
        this.store.listFriendIds(request.senderPlayerId),
      ]);
      if (mine.length >= FRIENDS.maxFriends || theirs.length >= FRIENDS.maxFriends) {
        return fail(ErrorCode.FriendLimitReached, "Arkadaş sınırı");
      }
      await this.store.addFriendPair(playerId, request.senderPlayerId, now);
      this.notify({ type: "friend.added", to: request.senderPlayerId, from: playerId });
    }
    await this.store.setFriendRequestStatus(requestId, outcome.value.status, now);
    return ok(undefined);
  }

  expireOverdue(): Promise<number> {
    return this.store.expireOverdueRequests(this.clock.now());
  }

  private async canReceive(senderId: PlayerId, receiverId: PlayerId, now: Date): Promise<boolean> {
    const settings = await this.store.getSettings(receiverId);
    if (!settings?.friendsEnabled) return false;
    if (await this.store.isBlockedEitherWay(senderId, receiverId)) return false;
    if (await this.store.findPendingBetween(senderId, receiverId)) return false;
    if ((await this.store.listFriendIds(receiverId)).length >= FRIENDS.maxFriends) return false;
    const rejectedAt = await this.store.lastRejectedAt(senderId, receiverId);
    if (rejectedAt && now.getTime() - rejectedAt.getTime() < FRIENDS.rejectedCooldownMs) return false;
    return true;
  }
}

export type { FriendRequest };
