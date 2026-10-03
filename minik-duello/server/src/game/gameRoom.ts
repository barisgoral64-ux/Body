import { randomBytes, timingSafeEqual } from "node:crypto";
import { LIMITS, ROOM, ROUND_TIMING } from "../config/constants.js";
import type { RandomInt } from "../domain/identity.js";
import { computeOutcomes, type PlayerOutcome } from "../domain/matchRewards.js";
import type { GameMode, MatchEndReason, PlayerId, RoomState } from "../domain/models.js";
import { scoreAnswer } from "../domain/scoring.js";
import type { QuickChatId } from "../domain/quickChat.js";
import type { CancelFn, Clock, IdGenerator, Scheduler } from "../infra/runtime.js";
import type { Logger } from "../infra/logger.js";
import { serverMessage, type RoundKind, type ServerMessage, type ServerMessageType } from "../protocol.js";
import { ErrorCode, fail, ok, type Result } from "../shared/result.js";
import { buildRound, isCoopMode, planRounds, type BuiltRound } from "./tasks.js";

export interface FinishedMatch {
  readonly matchId: string;
  readonly roomId: string;
  readonly mode: GameMode;
  readonly startedAt: Date;
  readonly endedAt: Date;
  readonly reason: MatchEndReason;
  readonly outcomes: readonly PlayerOutcome[];
  readonly coop: { readonly success: boolean; readonly progress: number; readonly target: number } | null;
}

export interface RoomDeps {
  readonly clock: Clock;
  readonly scheduler: Scheduler;
  readonly random: RandomInt;
  readonly newId: IdGenerator;
  readonly logger: Logger;
  emit(to: PlayerId, message: ServerMessage): void;
  /** Ödülleri uygular ve maçı kaydeder; oyuncu başına yeni ödül kimliklerini döndürür. */
  persist(match: FinishedMatch): Promise<ReadonlyMap<PlayerId, readonly string[]>>;
  onClosed(roomId: string): void;
}

interface ActiveRound {
  readonly built: BuiltRound;
  readonly index: number;
  acceptFromMs: number;
  deadlineMs: number;
  readonly answered: Set<PlayerId>;
  readonly claimed: Map<string, PlayerId>;
  readonly roundPoints: Map<PlayerId, number>;
  solved: boolean;
}

interface PausedState {
  readonly round: { readonly acceptOffsetMs: number; readonly remainingMs: number } | null;
  readonly nextIndex: number | null;
}

export interface AnswerAck {
  readonly roundId: string;
  readonly correct: boolean;
  readonly taken: boolean;
  readonly points: number;
  readonly total: number;
}

const COOP_STARS_TARGET = ROOM.coopTeamTargetStars;

export class GameRoom {
  readonly players: readonly [PlayerId, PlayerId];
  state: RoomState = "waiting";
  /** Test ve sunucu için: maç sonu kalıcılaştırma tamamlanınca çözülür. */
  persisted: Promise<void> = Promise.resolve();

  private readonly plan: readonly RoundKind[];
  private readonly resumeTokens = new Map<PlayerId, string>();
  private readonly connected = new Set<PlayerId>();
  private readonly ready = new Set<PlayerId>();
  private readonly scores = new Map<PlayerId, number>();
  private readonly streaks = new Map<PlayerId, number>();
  private readonly lastChatAt = new Map<PlayerId, number>();

  private roundIndex = -1;
  private roundsStarted = 0;
  private teamProgress = 0;
  private current: ActiveRound | null = null;
  private pendingNextIndex: number | null = null;
  private paused: PausedState | null = null;
  private startedAt: Date | null = null;

  private cancelWait: CancelFn | null = null;
  private cancelCountdown: CancelFn | null = null;
  private cancelRound: CancelFn | null = null;
  private cancelNext: CancelFn | null = null;
  private cancelGrace: CancelFn | null = null;

  constructor(
    readonly roomId: string,
    player1: PlayerId,
    player2: PlayerId,
    readonly mode: GameMode,
    private readonly deps: RoomDeps,
  ) {
    this.players = [player1, player2];
    this.plan = planRounds(mode);
    for (const p of this.players) {
      this.scores.set(p, 0);
      this.streaks.set(p, 0);
      this.resumeTokens.set(p, randomBytes(24).toString("hex"));
    }
    this.armWaitTimeout();
  }

  hasPlayer(playerId: PlayerId): boolean {
    return this.players.includes(playerId);
  }

  join(playerId: PlayerId): Result<void> {
    if (!this.hasPlayer(playerId)) return fail(ErrorCode.Forbidden, "Odanın üyesi değilsin");
    if (this.state === "finished") return fail(ErrorCode.InvalidState, "Oda kapandı");
    this.connected.add(playerId);
    this.send(playerId, "room.state", this.snapshot(playerId));
    return ok(undefined);
  }

  setReady(playerId: PlayerId): Result<void> {
    if (!this.hasPlayer(playerId)) return fail(ErrorCode.Forbidden, "Odanın üyesi değilsin");
    if (this.state !== "waiting") return fail(ErrorCode.InvalidState, "Oda hazır değil");
    this.ready.add(playerId);
    this.broadcast("room.ready", { playerId });
    if (this.ready.size === ROOM.playersPerRoom && this.connected.size === ROOM.playersPerRoom) this.startCountdown();
    return ok(undefined);
  }

  answer(playerId: PlayerId, roundId: string, choiceId: string): Result<AnswerAck> {
    const round = this.current;
    if (!this.hasPlayer(playerId)) return fail(ErrorCode.Forbidden, "Odanın üyesi değilsin");
    if (this.state !== "playing" || !round || round.built.view.roundId !== roundId) {
      return fail(ErrorCode.InvalidState, "Tur aktif değil");
    }
    const nowMs = this.deps.clock.now().getTime();
    if (nowMs < round.acceptFromMs || nowMs > round.deadlineMs) return fail(ErrorCode.InvalidState, "Şu an cevap verilemez");
    const view = round.built.view;
    if (!view.choices.some((c) => c.id === choiceId)) return fail(ErrorCode.InvalidInput, "Geçersiz seçim");

    let taken = false;
    if (view.multi) {
      taken = round.claimed.has(choiceId);
    } else if (round.answered.has(playerId)) {
      return fail(ErrorCode.InvalidState, "Zaten cevapladın");
    } else {
      round.answered.add(playerId);
    }

    const correct = !taken && round.built.correct.has(choiceId);
    if (correct && view.multi) round.claimed.set(choiceId, playerId);

    const result = scoreAnswer({
      correct,
      // Süreyi yalnızca sunucu ölçer; istemci zaman damgasına güvenilmez.
      responseMs: nowMs - round.acceptFromMs,
      roundDurationMs: view.durationMs,
      currentStreak: this.streaks.get(playerId) ?? 0,
    });
    this.streaks.set(playerId, result.newStreak);
    const total = (this.scores.get(playerId) ?? 0) + result.points;
    this.scores.set(playerId, total);
    round.roundPoints.set(playerId, (round.roundPoints.get(playerId) ?? 0) + result.points);

    if (correct) this.onCorrect(round);
    const ack: AnswerAck = { roundId, correct, taken, points: result.points, total };
    this.send(playerId, "room.answerAck", { ...ack });
    if (this.isRoundComplete(round)) this.endRound();
    return ok(ack);
  }

  sendQuickChat(playerId: PlayerId, message: QuickChatId): Result<void> {
    if (!this.hasPlayer(playerId)) return fail(ErrorCode.Forbidden, "Odanın üyesi değilsin");
    if (this.state === "finished") return fail(ErrorCode.InvalidState, "Oda kapandı");
    const now = this.deps.clock.now().getTime();
    if (now - (this.lastChatAt.get(playerId) ?? -Infinity) < LIMITS.quickChatCooldownMs) {
      return fail(ErrorCode.RateLimited, "Biraz yavaş");
    }
    this.lastChatAt.set(playerId, now);
    const other = this.opponentOf(playerId);
    this.send(other, "room.quickChat", { from: playerId, message });
    return ok(undefined);
  }

  /** Bağlantı koptu: oyun duraklar, 15 sn içinde dönülmezse güvenli şekilde biter. */
  disconnect(playerId: PlayerId): void {
    if (!this.connected.delete(playerId) || this.state === "finished") return;

    if (this.state === "ready") {
      this.cancelCountdown?.();
      this.ready.clear();
      this.state = "waiting";
      this.armWaitTimeout();
      return;
    }
    if (this.state !== "playing") return;

    const nowMs = this.deps.clock.now().getTime();
    this.cancelRound?.();
    this.cancelNext?.();
    const round = this.current;
    this.paused = {
      round: round
        ? { acceptOffsetMs: Math.max(0, round.acceptFromMs - nowMs), remainingMs: Math.max(0, round.deadlineMs - Math.max(nowMs, round.acceptFromMs)) }
        : null,
      nextIndex: this.pendingNextIndex,
    };
    this.state = "reconnecting";
    this.send(this.opponentOf(playerId), "room.opponentDisconnected", { graceMs: ROOM.reconnectGraceMs });
    this.cancelGrace = this.deps.scheduler.after(ROOM.reconnectGraceMs, () => this.finish("disconnect"));
  }

  resume(playerId: PlayerId, token: string): Result<void> {
    if (!this.hasPlayer(playerId)) return fail(ErrorCode.Forbidden, "Odanın üyesi değilsin");
    if (this.state === "finished") return fail(ErrorCode.InvalidState, "Oda kapandı");
    const expected = Buffer.from(this.resumeTokens.get(playerId) ?? "");
    const given = Buffer.from(token);
    if (expected.length !== given.length || !timingSafeEqual(expected, given)) return fail(ErrorCode.Forbidden, "Geçersiz devam anahtarı");

    this.connected.add(playerId);
    if (this.state === "reconnecting" && this.connected.size === ROOM.playersPerRoom) {
      this.cancelGrace?.();
      this.state = "playing";
      this.restorePaused();
      this.broadcast("room.opponentReconnected", { playerId });
    }
    this.send(playerId, "room.state", this.snapshot(playerId));
    this.resendCurrentRound(playerId);
    return ok(undefined);
  }

  leave(playerId: PlayerId): void {
    if (this.hasPlayer(playerId)) this.finish("disconnect");
  }

  /** Dış nedenle (ör. ebeveyn engeli) kapatma. */
  close(reason: MatchEndReason): void {
    this.finish(reason);
  }

  /** Dönen oyuncuya aktif turu kalan süreyle yeniden gönderir; ekranı boş kalmaz. */
  private resendCurrentRound(playerId: PlayerId): void {
    const round = this.current;
    if (!round || this.state !== "playing") return;
    const nowMs = this.deps.clock.now().getTime();
    this.send(playerId, "room.round", {
      ...round.built.view,
      roundIndex: round.index,
      totalRounds: this.plan.length,
      showMs: Math.max(0, round.acceptFromMs - nowMs),
      durationMs: Math.max(0, round.deadlineMs - Math.max(nowMs, round.acceptFromMs)),
      resumed: true,
    });
  }

  private onCorrect(round: ActiveRound): void {
    if (this.mode === "coopStars") this.teamProgress += 1;
    if (this.mode === "coopPuzzle" && !round.solved) {
      round.solved = true;
      this.teamProgress += 1;
    }
  }

  private isRoundComplete(round: ActiveRound): boolean {
    if (this.mode === "coopStars" && this.teamProgress >= COOP_STARS_TARGET) return true;
    if (this.mode === "coopPuzzle" && round.solved) return true;
    if (round.built.view.multi) return round.claimed.size >= round.built.correct.size;
    return round.answered.size >= ROOM.playersPerRoom;
  }

  private startCountdown(): void {
    this.cancelWait?.();
    this.state = "ready";
    this.broadcast("room.countdown", { seconds: ROOM.countdownSeconds });
    this.cancelCountdown = this.deps.scheduler.after(ROOM.countdownSeconds * 1000, () => this.beginRound(0));
  }

  private beginRound(index: number): void {
    const kind = this.plan[index];
    if (!kind || this.state === "finished") return;
    this.pendingNextIndex = null;
    this.state = "playing";
    this.startedAt ??= this.deps.clock.now();
    this.roundIndex = index;
    this.roundsStarted += 1;

    const built = buildRound(kind, this.deps.random, this.deps.newId);
    const nowMs = this.deps.clock.now().getTime();
    const acceptFromMs = nowMs + built.view.showMs;
    this.current = {
      built, index, acceptFromMs, deadlineMs: acceptFromMs + built.view.durationMs,
      answered: new Set(), claimed: new Map(), roundPoints: new Map(), solved: false,
    };
    this.broadcast("room.round", { ...built.view, roundIndex: index, totalRounds: this.plan.length });
    this.cancelRound = this.deps.scheduler.after(built.view.showMs + built.view.durationMs, () => this.endRound());
  }

  private endRound(): void {
    const round = this.current;
    if (!round || this.state !== "playing") return;
    this.cancelRound?.();
    this.current = null;

    const record = (m: ReadonlyMap<PlayerId, number>): Record<PlayerId, number> => Object.fromEntries(this.players.map((p) => [p, m.get(p) ?? 0]));
    this.broadcast("room.roundResult", {
      roundId: round.built.view.roundId,
      correctChoiceIds: [...round.built.correct],
      claimedBy: Object.fromEntries(round.claimed),
      roundPoints: record(round.roundPoints),
      scores: record(this.scores),
      team: isCoopMode(this.mode) ? { progress: this.teamProgress, target: this.coopTarget() } : null,
    });

    const nextIndex = round.index + 1;
    const coopDone = this.mode === "coopStars" && this.teamProgress >= COOP_STARS_TARGET;
    if (coopDone || nextIndex >= this.plan.length) {
      this.finish("completed");
      return;
    }
    this.pendingNextIndex = nextIndex;
    this.cancelNext = this.deps.scheduler.after(ROUND_TIMING.betweenRoundsMs, () => this.beginRound(nextIndex));
  }

  private restorePaused(): void {
    const paused = this.paused;
    this.paused = null;
    if (!paused) return;
    if (paused.round && this.current) {
      const nowMs = this.deps.clock.now().getTime();
      this.current.acceptFromMs = nowMs + paused.round.acceptOffsetMs;
      this.current.deadlineMs = this.current.acceptFromMs + paused.round.remainingMs;
      this.cancelRound = this.deps.scheduler.after(paused.round.acceptOffsetMs + paused.round.remainingMs, () => this.endRound());
    } else if (paused.nextIndex !== null) {
      const next = paused.nextIndex;
      this.cancelNext = this.deps.scheduler.after(ROUND_TIMING.betweenRoundsMs, () => this.beginRound(next));
    }
  }

  private finish(reason: MatchEndReason): void {
    if (this.state === "finished") return;
    for (const cancel of [this.cancelWait, this.cancelCountdown, this.cancelRound, this.cancelNext, this.cancelGrace]) cancel?.();
    this.state = "finished";
    this.current = null;

    const coopTarget = this.coopTarget();
    const coopSuccess = isCoopMode(this.mode) && this.teamProgress >= coopTarget;
    const endedAt = this.deps.clock.now();
    const match: FinishedMatch = {
      matchId: this.deps.newId(),
      roomId: this.roomId,
      mode: this.mode,
      startedAt: this.startedAt ?? endedAt,
      endedAt,
      reason,
      outcomes: computeOutcomes({
        mode: this.mode, reason, players: this.players, roundsStarted: this.roundsStarted, coopSuccess,
        scores: Object.fromEntries(this.players.map((p) => [p, this.scores.get(p) ?? 0])),
      }),
      coop: isCoopMode(this.mode) ? { success: coopSuccess, progress: this.teamProgress, target: coopTarget } : null,
    };

    this.persisted = this.deps
      .persist(match)
      .catch((error: unknown) => {
        this.deps.logger.error("match_persist_failed", { roomId: this.roomId, message: error instanceof Error ? error.message : "unknown" });
        return new Map<PlayerId, readonly string[]>();
      })
      .then((newRewards) => {
        for (const p of this.players) {
          this.send(p, "room.finished", {
            matchId: match.matchId, reason, outcomes: match.outcomes, coop: match.coop, newRewards: newRewards.get(p) ?? [],
          });
        }
        this.deps.onClosed(this.roomId);
      });
  }

  private coopTarget(): number {
    return this.mode === "coopStars" ? COOP_STARS_TARGET : this.plan.length;
  }

  private armWaitTimeout(): void {
    this.cancelWait?.();
    this.cancelWait = this.deps.scheduler.after(ROUND_TIMING.roomWaitTimeoutMs, () => {
      if (this.state === "waiting") this.finish("timeout");
    });
  }

  private opponentOf(playerId: PlayerId): PlayerId {
    return playerId === this.players[0] ? this.players[1] : this.players[0];
  }

  private snapshot(forPlayer: PlayerId): Record<string, unknown> {
    return {
      roomId: this.roomId,
      mode: this.mode,
      state: this.state,
      players: this.players,
      currentRound: this.roundIndex,
      totalRounds: this.plan.length,
      scores: Object.fromEntries(this.players.map((p) => [p, this.scores.get(p) ?? 0])),
      opponentConnected: this.connected.has(this.opponentOf(forPlayer)),
      resumeToken: this.resumeTokens.get(forPlayer) ?? "",
    };
  }

  private send(to: PlayerId, type: ServerMessageType, payload: Record<string, unknown>): void {
    this.deps.emit(to, serverMessage(type, payload));
  }

  private broadcast(type: ServerMessageType, payload: Record<string, unknown>): void {
    for (const p of this.players) this.send(p, type, payload);
  }
}
