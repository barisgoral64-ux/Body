import type {
  FriendRequest, FriendRequestStatus, GameInvite, GameMode, InviteStatus, MatchEndReason, MatchResult,
  ParentSettings, Player, PlayerId, PlayerProfile,
} from "../domain/models.js";

export interface StoredParentSettings extends ParentSettings {
  readonly pinHash: string | null;
  readonly pinFailedAttempts: number;
  readonly pinLockedUntil: Date | null;
}

export interface LevelProgress {
  readonly playerId: PlayerId;
  readonly levelId: number;
  readonly stars: number;
  readonly bestScore: number;
  readonly completedAt: Date | null;
}

export interface MatchRecord {
  readonly matchId: string;
  readonly roomId: string;
  readonly gameMode: GameMode;
  readonly startedAt: Date;
  readonly endedAt: Date;
  readonly endReason: MatchEndReason;
}

export interface DailyClaimState {
  readonly claimCount: number;
  readonly lastClaimDay: string;
}

export type ProfilePatch = Partial<Pick<PlayerProfile, "avatarCharacter" | "avatarFrame" | "level">>;

/**
 * Kalıcılık sınırı. Servisler yalnızca bu arayüzü bilir; üretimde PostgreSQL, testte bellek içi uygulama kullanılır.
 */
export interface DataStore {
  createPlayer(player: Player, deviceHash: string, profile: PlayerProfile, settings: StoredParentSettings): Promise<void>;
  getPlayer(id: PlayerId): Promise<Player | null>;
  getPlayerByDeviceHash(hash: string): Promise<Player | null>;
  getPlayerByFriendCode(code: string): Promise<Player | null>;
  /** Kod başkasında ise false. */
  setFriendCode(id: PlayerId, code: string): Promise<boolean>;
  touchPlayer(id: PlayerId, at: Date): Promise<void>;
  getProfile(id: PlayerId): Promise<PlayerProfile | null>;
  updateProfile(id: PlayerId, patch: ProfilePatch): Promise<void>;

  getSettings(id: PlayerId): Promise<StoredParentSettings | null>;
  saveSettings(settings: StoredParentSettings): Promise<void>;

  addFriendPair(a: PlayerId, b: PlayerId, at: Date): Promise<void>;
  removeFriendPair(a: PlayerId, b: PlayerId): Promise<void>;
  areFriends(a: PlayerId, b: PlayerId): Promise<boolean>;
  listFriendIds(id: PlayerId): Promise<readonly PlayerId[]>;

  insertFriendRequest(request: FriendRequest): Promise<void>;
  getFriendRequest(id: string): Promise<FriendRequest | null>;
  setFriendRequestStatus(id: string, status: FriendRequestStatus, at: Date): Promise<void>;
  listPendingIncoming(receiver: PlayerId, now: Date): Promise<readonly FriendRequest[]>;
  findPendingBetween(a: PlayerId, b: PlayerId): Promise<FriendRequest | null>;
  lastRejectedAt(sender: PlayerId, receiver: PlayerId): Promise<Date | null>;
  countRequestsSentSince(sender: PlayerId, since: Date): Promise<number>;
  logCodeAttempt(sender: PlayerId, at: Date): Promise<void>;
  countCodeAttemptsSince(sender: PlayerId, since: Date): Promise<number>;
  expireOverdueRequests(now: Date): Promise<number>;
  cancelPendingBetween(a: PlayerId, b: PlayerId, at: Date): Promise<void>;

  addBlock(player: PlayerId, blocked: PlayerId, at: Date): Promise<void>;
  removeBlock(player: PlayerId, blocked: PlayerId): Promise<void>;
  isBlockedEitherWay(a: PlayerId, b: PlayerId): Promise<boolean>;
  listBlocked(player: PlayerId): Promise<readonly PlayerId[]>;

  insertInvite(invite: GameInvite): Promise<void>;
  getInvite(id: string): Promise<GameInvite | null>;
  setInviteStatus(id: string, status: InviteStatus): Promise<void>;
  findPendingInviteFrom(sender: PlayerId, now: Date): Promise<GameInvite | null>;
  lastInviteCreatedAt(sender: PlayerId): Promise<Date | null>;

  saveMatch(record: MatchRecord, results: readonly MatchResult[]): Promise<void>;

  /** Bakiyeyi günceller ve defter kaydı yazar; yeni bakiyeyi döndürür. */
  addCoins(id: PlayerId, delta: number, reason: string, refId: string | null): Promise<number>;
  /** Yeterli bakiye yoksa hiçbir şey yapmadan null döner (atomik; çift harcama olmaz). */
  spendCoins(id: PlayerId, amount: number, reason: string, refId: string | null): Promise<number | null>;
  /** Aynı oyuncunun ödül/ilerleme yazmalarını sıraya sokar (eşzamanlı çift istek koruması). */
  withPlayerLock<T>(id: PlayerId, fn: () => Promise<T>): Promise<T>;
  /** Yıldızı profile ekler, haftalık olay yazar; yeni toplamı döndürür. */
  addStars(id: PlayerId, stars: number, at: Date): Promise<number>;
  weeklyStars(ids: readonly PlayerId[], since: Date): Promise<ReadonlyMap<PlayerId, number>>;
  grantInventory(id: PlayerId, rewardId: string, at: Date): Promise<boolean>;
  listInventory(id: PlayerId): Promise<readonly { rewardId: string; equipped: boolean }[]>;
  setEquipped(id: PlayerId, rewardId: string, equipped: boolean): Promise<void>;

  getProgress(id: PlayerId, levelId: number): Promise<LevelProgress | null>;
  upsertProgress(progress: LevelProgress): Promise<void>;
  listProgress(id: PlayerId): Promise<readonly LevelProgress[]>;

  getDailyClaim(id: PlayerId): Promise<DailyClaimState | null>;
  putDailyClaim(id: PlayerId, state: DailyClaimState): Promise<void>;

  getSave(id: PlayerId): Promise<{ data: unknown; updatedAt: Date } | null>;
  putSave(id: PlayerId, data: unknown, at: Date): Promise<void>;
}
