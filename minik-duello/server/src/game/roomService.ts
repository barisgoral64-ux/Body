import type { RandomInt } from "../domain/identity.js";
import type { GameMode, PlayerId } from "../domain/models.js";
import type { Logger } from "../infra/logger.js";
import type { Clock, IdGenerator, Scheduler } from "../infra/runtime.js";
import type { DataStore } from "../infra/store.js";
import type { ServerMessage } from "../protocol.js";
import { ErrorCode, fail, ok, type Result } from "../shared/result.js";
import type { RewardService } from "../services/rewardService.js";
import { GameRoom, type FinishedMatch } from "./gameRoom.js";

export type Emitter = (to: PlayerId, message: ServerMessage) => void;

/** Odaları yönetir ve maç sonucunu (ödül + kayıt) kalıcılaştırır. Bir oyuncu aynı anda tek odada olabilir. */
export class RoomService {
  private readonly rooms = new Map<string, GameRoom>();
  private readonly byPlayer = new Map<PlayerId, string>();
  /** Gateway bağlanınca atanır. */
  private emitter: Emitter = () => undefined;

  constructor(
    private readonly store: DataStore,
    private readonly rewards: RewardService,
    private readonly clock: Clock,
    private readonly scheduler: Scheduler,
    private readonly random: RandomInt,
    private readonly newId: IdGenerator,
    private readonly logger: Logger,
    private readonly onRoomClosed: (players: readonly PlayerId[]) => void = () => undefined,
  ) {}

  setEmitter(emitter: Emitter): void {
    this.emitter = emitter;
  }

  roomOf(playerId: PlayerId): GameRoom | null {
    const id = this.byPlayer.get(playerId);
    return id ? (this.rooms.get(id) ?? null) : null;
  }

  get(roomId: string): GameRoom | null {
    return this.rooms.get(roomId) ?? null;
  }

  activeRoomCount(): number {
    return this.rooms.size;
  }

  /** İki oyuncu da bağlı olmalıdır (davet kabulünde doğrulanır). */
  createRoom(player1: PlayerId, player2: PlayerId, mode: GameMode): Result<GameRoom> {
    if (player1 === player2) return fail(ErrorCode.InvalidInput, "Aynı oyuncu");
    if (this.byPlayer.has(player1) || this.byPlayer.has(player2)) return fail(ErrorCode.InvalidState, "Oyuncu zaten odada");

    const roomId = this.newId();
    const room = new GameRoom(roomId, player1, player2, mode, {
      clock: this.clock,
      scheduler: this.scheduler,
      random: this.random,
      newId: this.newId,
      logger: this.logger.child("room"),
      emit: (to, message) => this.emitter(to, message),
      persist: (match) => this.persist(match),
      onClosed: (id) => this.close(id),
    });
    this.rooms.set(roomId, room);
    this.byPlayer.set(player1, roomId);
    this.byPlayer.set(player2, roomId);
    room.join(player1);
    room.join(player2);
    return ok(room);
  }

  /** İlişki kaldırıldığında (engel/arkadaş silme) ortak odayı güvenle kapatır. */
  endRoomBetween(a: PlayerId, b: PlayerId): void {
    const room = this.roomOf(a);
    if (room && room.hasPlayer(b)) room.close("disconnect");
  }

  private close(roomId: string): void {
    const room = this.rooms.get(roomId);
    if (!room) return;
    this.rooms.delete(roomId);
    for (const p of room.players) if (this.byPlayer.get(p) === roomId) this.byPlayer.delete(p);
    this.onRoomClosed(room.players);
  }

  private async persist(match: FinishedMatch): Promise<ReadonlyMap<PlayerId, readonly string[]>> {
    await this.store.saveMatch(
      { matchId: match.matchId, roomId: match.roomId, gameMode: match.mode, startedAt: match.startedAt, endedAt: match.endedAt, endReason: match.reason },
      match.outcomes.map((o) => ({ matchId: match.matchId, ...o })),
    );
    const newRewards = new Map<PlayerId, readonly string[]>();
    for (const o of match.outcomes) {
      const summary = await this.rewards.grant(o.playerId, o.starsAwarded, o.coinsAwarded, "match", match.matchId);
      newRewards.set(o.playerId, summary.newRewards);
    }
    return newRewards;
  }
}
