export type PlayerId = string;

export type CharacterId = "panda" | "rabbit" | "cat" | "dog" | "dinosaur" | "fox" | "koala" | "penguin";
export const CHARACTER_IDS: readonly CharacterId[] = [
  "panda", "rabbit", "cat", "dog", "dinosaur", "fox", "koala", "penguin",
];

export interface Player {
  readonly playerId: PlayerId;
  readonly friendCode: string;
  readonly username: string;
  readonly createdAt: Date;
  readonly lastSeenAt: Date;
}

export interface PlayerProfile {
  readonly playerId: PlayerId;
  readonly avatarCharacter: CharacterId;
  readonly avatarFrame: string | null;
  readonly level: number;
  readonly totalStars: number;
  readonly coins: number;
}

export type FriendRequestStatus = "pending" | "accepted" | "rejected" | "expired";

export interface FriendRequest {
  readonly requestId: string;
  readonly senderPlayerId: PlayerId;
  readonly receiverPlayerId: PlayerId;
  readonly status: FriendRequestStatus;
  readonly createdAt: Date;
  readonly expiresAt: Date;
}

export type GameMode =
  | "colorRace" | "shapeRace" | "numberRace" | "memoryDuel" | "puzzleRace" | "starCollect"
  | "coopStars" | "coopPuzzle" | "mixedMatch";

export const COOP_MODES: readonly GameMode[] = ["coopStars", "coopPuzzle"];

export type InviteStatus = "pending" | "accepted" | "declined" | "expired";

export interface GameInvite {
  readonly inviteId: string;
  readonly senderId: PlayerId;
  readonly receiverId: PlayerId;
  readonly gameMode: GameMode;
  readonly createdAt: Date;
  readonly expiresAt: Date;
  readonly status: InviteStatus;
}

export type RoomState = "waiting" | "ready" | "playing" | "reconnecting" | "finished";
export type MatchEndReason = "completed" | "disconnect" | "timeout" | "declined";

export interface Room {
  readonly roomId: string;
  readonly player1: PlayerId;
  readonly player2: PlayerId;
  readonly gameMode: GameMode;
  readonly gameState: RoomState;
  readonly currentRound: number;
  readonly scores: Readonly<Record<PlayerId, number>>;
  readonly createdAt: Date;
}

export interface MatchResult {
  readonly matchId: string;
  readonly playerId: PlayerId;
  readonly score: number;
  readonly isWinner: boolean;
  readonly starsAwarded: number;
  readonly coinsAwarded: number;
}

export interface ParentSettings {
  readonly playerId: PlayerId;
  readonly friendsEnabled: boolean;
  readonly multiplayerEnabled: boolean;
  readonly onlineStatusVisible: boolean;
  readonly gameInvitationsEnabled: boolean;
  readonly dailyLimitMinutes: number;
  readonly soundEnabled: boolean;
}

/** Güvenli varsayılan: sosyal özelliklerin tamamı KAPALI, ebeveyn açar. */
export function defaultParentSettings(playerId: PlayerId): ParentSettings {
  return {
    playerId,
    friendsEnabled: false,
    multiplayerEnabled: false,
    onlineStatusVisible: false,
    gameInvitationsEnabled: false,
    dailyLimitMinutes: 0,
    soundEnabled: true,
  };
}

export type PresenceStatus = "online" | "playing" | "offline";
