import type { Result } from "../shared/result.js";
import type {
  FriendRequest, GameInvite, GameMode, MatchResult, ParentSettings, Player, PlayerId, PlayerProfile,
  PresenceStatus, Room,
} from "../domain/models.js";
import type { QuickChatId } from "../domain/quickChat.js";

export interface AuthTokens {
  readonly accessToken: string;
  readonly refreshToken: string;
}

export interface IAuthService {
  /** Anonim cihaz hesabı: e-posta/telefon alınmaz. */
  registerAnonymous(deviceId: string): Promise<Result<{ player: Player; tokens: AuthTokens }>>;
  refresh(refreshToken: string): Promise<Result<AuthTokens>>;
  verifyAccessToken(accessToken: string): Result<PlayerId>;
}

export interface IPlayerService {
  getPlayer(playerId: PlayerId): Promise<Result<Player>>;
  getProfile(playerId: PlayerId): Promise<Result<PlayerProfile>>;
  findByFriendCode(friendCode: string): Promise<Player | null>;
  regenerateFriendCode(playerId: PlayerId): Promise<Result<string>>;
}

export interface IFriendService {
  listFriends(playerId: PlayerId): Promise<Result<readonly Player[]>>;
  areFriends(a: PlayerId, b: PlayerId): Promise<boolean>;
  removeFriend(playerId: PlayerId, friendId: PlayerId): Promise<Result<void>>;
}

export interface IFriendRequestService {
  send(senderId: PlayerId, friendCode: string): Promise<Result<void>>;
  listIncoming(playerId: PlayerId): Promise<Result<readonly FriendRequest[]>>;
  respond(playerId: PlayerId, requestId: string, accept: boolean): Promise<Result<void>>;
  expireOverdue(now: Date): Promise<number>;
}

export interface IMultiplayerService {
  sendInvite(senderId: PlayerId, receiverId: PlayerId, mode: GameMode): Promise<Result<GameInvite>>;
  respondToInvite(playerId: PlayerId, inviteId: string, accept: boolean): Promise<Result<Room | null>>;
  sendQuickChat(roomId: string, senderId: PlayerId, message: QuickChatId): Promise<Result<void>>;
  setPresence(playerId: PlayerId, status: PresenceStatus): Promise<void>;
  getPresence(viewerId: PlayerId, targetId: PlayerId): Promise<PresenceStatus>;
}

export interface IMatchService {
  /** İstemci yalnızca seçimi gönderir; doğruluk, süre ve puan sunucuda belirlenir. */
  submitAnswer(roomId: string, playerId: PlayerId, roundId: string, choiceId: string): Promise<Result<void>>;
  finish(roomId: string): Promise<Result<readonly MatchResult[]>>;
}

export interface IRoomService {
  createRoom(player1: PlayerId, player2: PlayerId, mode: GameMode): Promise<Result<Room>>;
  markReady(roomId: string, playerId: PlayerId): Promise<Result<Room>>;
  markDisconnected(roomId: string, playerId: PlayerId): Promise<Result<Room>>;
  resume(roomId: string, playerId: PlayerId, resumeToken: string): Promise<Result<Room>>;
}

export interface IRewardService {
  grantMatchRewards(results: readonly MatchResult[]): Promise<Result<void>>;
}

export interface ILeaderboardService {
  weeklyFriendStars(playerId: PlayerId): Promise<Result<readonly { player: Player; stars: number }[]>>;
}

export interface IParentControlService {
  getSettings(playerId: PlayerId): Promise<Result<ParentSettings>>;
  updateSettings(playerId: PlayerId, pin: string, patch: Partial<ParentSettings>): Promise<Result<ParentSettings>>;
  setPin(playerId: PlayerId, newPin: string, currentPin?: string): Promise<Result<void>>;
  blockPlayer(playerId: PlayerId, pin: string, blockedId: PlayerId): Promise<Result<void>>;
  isBlocked(a: PlayerId, b: PlayerId): Promise<boolean>;
}

export interface ISaveService {
  load(playerId: PlayerId): Promise<Result<unknown>>;
  save(playerId: PlayerId, snapshot: unknown): Promise<Result<void>>;
}

export interface INotificationService {
  notify(playerId: PlayerId, event: { type: string; payload: Record<string, unknown> }): Promise<void>;
}
