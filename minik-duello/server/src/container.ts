import { randomInt } from "node:crypto";
import type { AppConfig } from "./config/env.js";
import type { RandomInt } from "./domain/identity.js";
import { RoomService } from "./game/roomService.js";
import type { Logger } from "./infra/logger.js";
import { systemClock, systemScheduler, uuid, type Clock, type IdGenerator, type Scheduler } from "./infra/runtime.js";
import type { DataStore } from "./infra/store.js";
import type { ServerMessage } from "./protocol.js";
import { TokenService } from "./security/tokens.js";
import { AuthService } from "./services/authService.js";
import { FriendRequestService } from "./services/friendRequestService.js";
import { FriendService } from "./services/friendService.js";
import { LeaderboardService } from "./services/leaderboardService.js";
import { LevelService } from "./services/levelService.js";
import { MultiplayerService } from "./services/multiplayerService.js";
import { ParentControlService } from "./services/parentControlService.js";
import { PlayerService } from "./services/playerService.js";
import { PresenceService } from "./services/presenceService.js";
import { RewardService } from "./services/rewardService.js";
import { SaveService } from "./services/saveService.js";

export interface ContainerOptions {
  readonly store: DataStore;
  readonly clock?: Clock;
  readonly scheduler?: Scheduler;
  readonly random?: RandomInt;
  readonly newId?: IdGenerator;
}

export interface PushSink {
  deliver(to: string, message: ServerMessage): void;
  /** Hesap silindi / oturum kapatıldı: bağlantıyı düşür. */
  disconnect(playerId: string): void;
  onRoomClosed(players: readonly string[]): void;
}

export interface Container {
  readonly config: AppConfig;
  readonly logger: Logger;
  readonly clock: Clock;
  readonly scheduler: Scheduler;
  readonly store: DataStore;
  readonly auth: AuthService;
  readonly players: PlayerService;
  readonly parent: ParentControlService;
  readonly friends: FriendService;
  readonly friendRequests: FriendRequestService;
  readonly presence: PresenceService;
  readonly rewards: RewardService;
  readonly levels: LevelService;
  readonly leaderboard: LeaderboardService;
  readonly saves: SaveService;
  readonly rooms: RoomService;
  readonly multiplayer: MultiplayerService;
  /** Gateway kendini buraya bağlar. */
  bindSink(sink: PushSink): void;
}

/** Tüm servisleri tek yerde bağlar (composition root). Testte sahte saat/zamanlayıcı verilebilir. */
export function createContainer(config: AppConfig, logger: Logger, options: ContainerOptions): Container {
  const clock = options.clock ?? systemClock;
  const scheduler = options.scheduler ?? systemScheduler;
  const random = options.random ?? ((max: number) => randomInt(max));
  const newId = options.newId ?? uuid;
  const store = options.store;

  let sink: PushSink | null = null;
  const deliver = (to: string, message: ServerMessage): void => sink?.deliver(to, message);

  const tokens = new TokenService(config.jwtSecret, clock);
  const auth = new AuthService(store, tokens, clock, newId, config.deviceSecret);
  const players = new PlayerService(store);
  const presence = new PresenceService(store, clock);
  const rewards = new RewardService(store, clock);
  const levels = new LevelService(store, rewards, clock);
  const leaderboard = new LeaderboardService(store, clock);
  const saves = new SaveService(store, clock);
  const friends = new FriendService(store, players, presence);

  const rooms = new RoomService(store, rewards, clock, scheduler, random, newId, logger, (p) => sink?.onRoomClosed(p));
  rooms.setEmitter(deliver);

  const parent = new ParentControlService(
    store,
    clock,
    (a, b) => rooms.endRoomBetween(a, b),
    (playerId) => {
      rooms.roomOf(playerId)?.leave(playerId);
      sink?.disconnect(playerId);
    },
  );
  const friendRequests = new FriendRequestService(store, players, clock, newId, (event) =>
    deliver(event.to, { v: 1, type: "friend.event", payload: { type: event.type, from: event.from } }),
  );
  const multiplayer = new MultiplayerService(store, players, presence, rooms, clock, newId, deliver);

  return {
    config, logger, clock, scheduler, store, auth, players, parent, friends, friendRequests, presence,
    rewards, levels, leaderboard, saves, rooms, multiplayer,
    bindSink(s) {
      sink = s;
    },
  };
}
