import { INVITE } from "../config/constants.js";
import type { GameInvite, GameMode, PlayerId } from "../domain/models.js";
import type { Clock, IdGenerator } from "../infra/runtime.js";
import type { DataStore } from "../infra/store.js";
import { serverMessage, type ServerMessage } from "../protocol.js";
import type { GameRoom } from "../game/gameRoom.js";
import type { RoomService } from "../game/roomService.js";
import { ErrorCode, fail, ok, type Result } from "../shared/result.js";
import type { PlayerService } from "./playerService.js";
import type { PresenceService } from "./presenceService.js";

export type InviteEmitter = (to: PlayerId, message: ServerMessage) => void;

/** Davet akışı. Alıcı tarafındaki red nedenleri gönderene nötr "OpponentUnavailable" olarak döner. */
export class MultiplayerService {
  constructor(
    private readonly store: DataStore,
    private readonly players: PlayerService,
    private readonly presence: PresenceService,
    private readonly rooms: RoomService,
    private readonly clock: Clock,
    private readonly newId: IdGenerator,
    private readonly emit: InviteEmitter,
  ) {}

  async sendInvite(senderId: PlayerId, receiverId: PlayerId, mode: GameMode): Promise<Result<GameInvite>> {
    if (senderId === receiverId) return fail(ErrorCode.InvalidInput, "Kendine davet olmaz");

    const own = await this.socialGate(senderId);
    if (!own.ok) return own;
    if (!(await this.store.areFriends(senderId, receiverId))) return fail(ErrorCode.Forbidden, "Arkadaş değilsiniz");
    if (this.rooms.roomOf(senderId)) return fail(ErrorCode.InvalidState, "Zaten bir oyundasın");

    const unavailable = fail<GameInvite>(ErrorCode.OpponentUnavailable, "Şu an oynayamıyor");
    if (!(await this.canBeInvited(senderId, receiverId))) return unavailable;

    const now = this.clock.now();
    if (await this.store.findPendingInviteFrom(senderId, now)) return fail(ErrorCode.RateLimited, "Bekleyen davetin var");
    const last = await this.store.lastInviteCreatedAt(senderId);
    if (last && now.getTime() - last.getTime() < INVITE.resendCooldownMs) return fail(ErrorCode.RateLimited, "Biraz bekle");

    const invite: GameInvite = {
      inviteId: this.newId(), senderId, receiverId, gameMode: mode, createdAt: now,
      expiresAt: new Date(now.getTime() + INVITE.expiresInMs), status: "pending",
    };
    await this.store.insertInvite(invite);
    const from = await this.players.getPublic(senderId);
    this.emit(receiverId, serverMessage("invite.received", {
      inviteId: invite.inviteId, from, mode, expiresAt: invite.expiresAt.toISOString(),
    }));
    return ok(invite);
  }

  /** Kabulde oda oluşturulur; reddedilirse gönderene yumuşak bir "şimdi değil" bildirilir. */
  async respondToInvite(playerId: PlayerId, inviteId: string, accept: boolean): Promise<Result<GameRoom | null>> {
    const invite = await this.store.getInvite(inviteId);
    if (!invite || invite.receiverId !== playerId) return fail(ErrorCode.NotFound, "Davet bulunamadı");
    if (invite.status !== "pending") return fail(ErrorCode.InvalidState, "Davet zaten yanıtlandı");

    const now = this.clock.now();
    if (invite.expiresAt <= now) {
      await this.store.setInviteStatus(inviteId, "expired");
      return fail(ErrorCode.InviteExpired, "Davetin süresi doldu");
    }

    if (!accept) {
      await this.store.setInviteStatus(inviteId, "declined");
      this.emit(invite.senderId, serverMessage("invite.resolved", { inviteId, status: "declined" }));
      return ok(null);
    }

    const gate = await this.socialGate(playerId);
    const stillValid =
      gate.ok &&
      this.presence.actual(invite.senderId) !== "offline" &&
      (await this.canBeInvited(invite.senderId, playerId)) &&
      !this.rooms.roomOf(playerId) &&
      !this.rooms.roomOf(invite.senderId);
    if (!stillValid) {
      await this.store.setInviteStatus(inviteId, "expired");
      this.emit(invite.senderId, serverMessage("invite.resolved", { inviteId, status: "declined" }));
      return fail(ErrorCode.OpponentUnavailable, "Şu an oynayamıyor");
    }

    await this.store.setInviteStatus(inviteId, "accepted");
    const room = this.rooms.createRoom(invite.senderId, playerId, invite.gameMode);
    if (!room.ok) return room;
    this.emit(invite.senderId, serverMessage("invite.resolved", { inviteId, status: "accepted", roomId: room.value.roomId }));
    return ok(room.value);
  }

  /** Oyuncunun kendi ebeveyn izinleri: arkadaş + multiplayer + davet açık olmalı. */
  private async socialGate(playerId: PlayerId): Promise<Result<void>> {
    const s = await this.store.getSettings(playerId);
    if (!s?.friendsEnabled) return fail(ErrorCode.FriendsDisabled, "Arkadaş sistemi kapalı");
    if (!s.multiplayerEnabled) return fail(ErrorCode.MultiplayerDisabled, "Çok oyunculu kapalı");
    if (!s.gameInvitationsEnabled) return fail(ErrorCode.InvitesDisabled, "Davetler kapalı");
    return ok(undefined);
  }

  private async canBeInvited(senderId: PlayerId, receiverId: PlayerId): Promise<boolean> {
    if (!(await this.socialGate(receiverId)).ok) return false;
    if (await this.store.isBlockedEitherWay(senderId, receiverId)) return false;
    if (this.presence.actual(receiverId) !== "online") return false;
    return !this.rooms.roomOf(receiverId);
  }
}
