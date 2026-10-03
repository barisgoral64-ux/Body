import type {
  FriendRequest, FriendRequestStatus, GameInvite, InviteStatus, MatchResult, Player, PlayerId, PlayerProfile,
} from "../domain/models.js";
import { orderFriendPair } from "../domain/friendRequest.js";
import { KeyedMutex } from "./mutex.js";
import type {
  DailyClaimState, DataStore, LevelProgress, MatchRecord, ProfilePatch, StoredParentSettings,
} from "./store.js";

interface RequestRow {
  request: FriendRequest;
  resolvedAt: Date | null;
}

const pairKey = (a: PlayerId, b: PlayerId): string => orderFriendPair(a, b).join("|");

/** Bellek içi DataStore: testler ve yerel geliştirme için. Süreç kapanınca veri kaybolur. */
export class MemoryStore implements DataStore {
  private readonly players = new Map<PlayerId, Player>();
  private readonly byDevice = new Map<string, PlayerId>();
  private readonly byCode = new Map<string, PlayerId>();
  private readonly profiles = new Map<PlayerId, PlayerProfile>();
  private readonly settings = new Map<PlayerId, StoredParentSettings>();
  private readonly friends = new Map<string, true>();
  private readonly requests = new Map<string, RequestRow>();
  private readonly blocks = new Set<string>();
  private readonly codeAttempts: { sender: PlayerId; at: Date }[] = [];
  private readonly invites = new Map<string, GameInvite>();
  private readonly matches: { record: MatchRecord; results: readonly MatchResult[] }[] = [];
  private readonly coinLedger: { id: PlayerId; delta: number; reason: string; refId: string | null }[] = [];
  private readonly starEvents: { id: PlayerId; stars: number; at: Date }[] = [];
  private readonly inventory = new Map<PlayerId, Map<string, boolean>>();
  private readonly progress = new Map<string, LevelProgress>();
  private readonly daily = new Map<PlayerId, DailyClaimState>();
  private readonly locks = new KeyedMutex();
  private readonly saves = new Map<PlayerId, { data: unknown; updatedAt: Date }>();

  createPlayer(player: Player, deviceHash: string, profile: PlayerProfile, settings: StoredParentSettings): Promise<void> {
    this.players.set(player.playerId, player);
    this.byDevice.set(deviceHash, player.playerId);
    this.byCode.set(player.friendCode, player.playerId);
    this.profiles.set(player.playerId, profile);
    this.settings.set(player.playerId, settings);
    return Promise.resolve();
  }
  getPlayer(id: PlayerId): Promise<Player | null> {
    return Promise.resolve(this.players.get(id) ?? null);
  }
  getPlayerByDeviceHash(hash: string): Promise<Player | null> {
    const id = this.byDevice.get(hash);
    return Promise.resolve(id ? (this.players.get(id) ?? null) : null);
  }
  getPlayerByFriendCode(code: string): Promise<Player | null> {
    const id = this.byCode.get(code);
    return Promise.resolve(id ? (this.players.get(id) ?? null) : null);
  }
  setFriendCode(id: PlayerId, code: string): Promise<boolean> {
    const owner = this.byCode.get(code);
    const player = this.players.get(id);
    if ((owner !== undefined && owner !== id) || !player) return Promise.resolve(false);
    this.byCode.delete(player.friendCode);
    this.byCode.set(code, id);
    this.players.set(id, { ...player, friendCode: code });
    return Promise.resolve(true);
  }
  touchPlayer(id: PlayerId, at: Date): Promise<void> {
    const p = this.players.get(id);
    if (p) this.players.set(id, { ...p, lastSeenAt: at });
    return Promise.resolve();
  }
  deletePlayer(id: PlayerId): Promise<void> {
    const player = this.players.get(id);
    if (player) this.byCode.delete(player.friendCode);
    for (const [hash, pid] of this.byDevice) if (pid === id) this.byDevice.delete(hash);
    for (const key of [...this.friends.keys()]) if (key.split("|").includes(id)) this.friends.delete(key);
    for (const [rid, row] of [...this.requests]) {
      if (row.request.senderPlayerId === id || row.request.receiverPlayerId === id) this.requests.delete(rid);
    }
    for (const key of [...this.blocks]) if (key.split(">").includes(id)) this.blocks.delete(key);
    for (const [iid, invite] of [...this.invites]) if (invite.senderId === id || invite.receiverId === id) this.invites.delete(iid);
    for (const [key, p] of [...this.progress]) if (p.playerId === id) this.progress.delete(key);
    for (let i = this.codeAttempts.length - 1; i >= 0; i -= 1) if (this.codeAttempts[i]?.sender === id) this.codeAttempts.splice(i, 1);
    for (let i = this.starEvents.length - 1; i >= 0; i -= 1) if (this.starEvents[i]?.id === id) this.starEvents.splice(i, 1);
    for (let i = this.coinLedger.length - 1; i >= 0; i -= 1) if (this.coinLedger[i]?.id === id) this.coinLedger.splice(i, 1);
    for (const m of this.matches) {
      (m as { results: readonly MatchResult[] }).results = m.results.filter((r) => r.playerId !== id);
    }
    this.players.delete(id);
    this.profiles.delete(id);
    this.settings.delete(id);
    this.inventory.delete(id);
    this.daily.delete(id);
    this.saves.delete(id);
    return Promise.resolve();
  }
  /** Test yardımcısı: oyuncuya ait herhangi bir kalıntı var mı? */
  hasAnyDataFor(id: PlayerId): boolean {
    const inMaps = [this.players, this.profiles, this.settings, this.inventory, this.daily, this.saves].some((m) => m.has(id));
    const inFriends = [...this.friends.keys()].some((k) => k.split("|").includes(id));
    const inRequests = [...this.requests.values()].some((r) => r.request.senderPlayerId === id || r.request.receiverPlayerId === id);
    const inBlocks = [...this.blocks].some((k) => k.split(">").includes(id));
    const inInvites = [...this.invites.values()].some((i) => i.senderId === id || i.receiverId === id);
    const inProgress = [...this.progress.values()].some((p) => p.playerId === id);
    const inMatches = this.matches.some((m) => m.results.some((r) => r.playerId === id));
    return inMaps || inFriends || inRequests || inBlocks || inInvites || inProgress || inMatches;
  }

  getProfile(id: PlayerId): Promise<PlayerProfile | null> {
    return Promise.resolve(this.profiles.get(id) ?? null);
  }
  updateProfile(id: PlayerId, patch: ProfilePatch): Promise<void> {
    const p = this.profiles.get(id);
    if (p) this.profiles.set(id, { ...p, ...patch });
    return Promise.resolve();
  }

  getSettings(id: PlayerId): Promise<StoredParentSettings | null> {
    return Promise.resolve(this.settings.get(id) ?? null);
  }
  saveSettings(settings: StoredParentSettings): Promise<void> {
    this.settings.set(settings.playerId, settings);
    return Promise.resolve();
  }

  addFriendPair(a: PlayerId, b: PlayerId): Promise<void> {
    this.friends.set(pairKey(a, b), true);
    return Promise.resolve();
  }
  removeFriendPair(a: PlayerId, b: PlayerId): Promise<void> {
    this.friends.delete(pairKey(a, b));
    return Promise.resolve();
  }
  areFriends(a: PlayerId, b: PlayerId): Promise<boolean> {
    return Promise.resolve(this.friends.has(pairKey(a, b)));
  }
  listFriendIds(id: PlayerId): Promise<readonly PlayerId[]> {
    const out: PlayerId[] = [];
    for (const key of this.friends.keys()) {
      const [a, b] = key.split("|") as [string, string];
      if (a === id) out.push(b);
      else if (b === id) out.push(a);
    }
    return Promise.resolve(out);
  }

  insertFriendRequest(request: FriendRequest): Promise<void> {
    this.requests.set(request.requestId, { request, resolvedAt: null });
    return Promise.resolve();
  }
  getFriendRequest(id: string): Promise<FriendRequest | null> {
    return Promise.resolve(this.requests.get(id)?.request ?? null);
  }
  setFriendRequestStatus(id: string, status: FriendRequestStatus, at: Date): Promise<void> {
    const row = this.requests.get(id);
    if (row) this.requests.set(id, { request: { ...row.request, status }, resolvedAt: at });
    return Promise.resolve();
  }
  listPendingIncoming(receiver: PlayerId, now: Date): Promise<readonly FriendRequest[]> {
    const out = [...this.requests.values()]
      .map((r) => r.request)
      .filter((r) => r.receiverPlayerId === receiver && r.status === "pending" && r.expiresAt.getTime() > now.getTime());
    return Promise.resolve(out);
  }
  findPendingBetween(a: PlayerId, b: PlayerId): Promise<FriendRequest | null> {
    for (const { request: r } of this.requests.values()) {
      const between =
        (r.senderPlayerId === a && r.receiverPlayerId === b) || (r.senderPlayerId === b && r.receiverPlayerId === a);
      if (between && r.status === "pending") return Promise.resolve(r);
    }
    return Promise.resolve(null);
  }
  lastRejectedAt(sender: PlayerId, receiver: PlayerId): Promise<Date | null> {
    let latest: Date | null = null;
    for (const row of this.requests.values()) {
      const r = row.request;
      if (r.senderPlayerId === sender && r.receiverPlayerId === receiver && r.status === "rejected" && row.resolvedAt) {
        if (!latest || row.resolvedAt > latest) latest = row.resolvedAt;
      }
    }
    return Promise.resolve(latest);
  }
  countRequestsSentSince(sender: PlayerId, since: Date): Promise<number> {
    let n = 0;
    for (const { request: r } of this.requests.values()) {
      if (r.senderPlayerId === sender && r.createdAt >= since) n += 1;
    }
    return Promise.resolve(n);
  }
  logCodeAttempt(sender: PlayerId, at: Date): Promise<void> {
    this.codeAttempts.push({ sender, at });
    return Promise.resolve();
  }
  countCodeAttemptsSince(sender: PlayerId, since: Date): Promise<number> {
    return Promise.resolve(this.codeAttempts.filter((a) => a.sender === sender && a.at >= since).length);
  }
  expireOverdueRequests(now: Date): Promise<number> {
    let n = 0;
    for (const [id, row] of this.requests) {
      if (row.request.status === "pending" && row.request.expiresAt <= now) {
        this.requests.set(id, { request: { ...row.request, status: "expired" }, resolvedAt: now });
        n += 1;
      }
    }
    return Promise.resolve(n);
  }
  cancelPendingBetween(a: PlayerId, b: PlayerId, at: Date): Promise<void> {
    for (const [id, row] of this.requests) {
      const r = row.request;
      const between =
        (r.senderPlayerId === a && r.receiverPlayerId === b) || (r.senderPlayerId === b && r.receiverPlayerId === a);
      if (between && r.status === "pending") this.requests.set(id, { request: { ...r, status: "rejected" }, resolvedAt: at });
    }
    return Promise.resolve();
  }

  addBlock(player: PlayerId, blocked: PlayerId): Promise<void> {
    this.blocks.add(`${player}>${blocked}`);
    return Promise.resolve();
  }
  removeBlock(player: PlayerId, blocked: PlayerId): Promise<void> {
    this.blocks.delete(`${player}>${blocked}`);
    return Promise.resolve();
  }
  isBlockedEitherWay(a: PlayerId, b: PlayerId): Promise<boolean> {
    return Promise.resolve(this.blocks.has(`${a}>${b}`) || this.blocks.has(`${b}>${a}`));
  }
  listBlocked(player: PlayerId): Promise<readonly PlayerId[]> {
    const prefix = `${player}>`;
    return Promise.resolve([...this.blocks].filter((k) => k.startsWith(prefix)).map((k) => k.slice(prefix.length)));
  }

  insertInvite(invite: GameInvite): Promise<void> {
    this.invites.set(invite.inviteId, invite);
    return Promise.resolve();
  }
  getInvite(id: string): Promise<GameInvite | null> {
    return Promise.resolve(this.invites.get(id) ?? null);
  }
  setInviteStatus(id: string, status: InviteStatus): Promise<void> {
    const i = this.invites.get(id);
    if (i) this.invites.set(id, { ...i, status });
    return Promise.resolve();
  }
  findPendingInviteFrom(sender: PlayerId, now: Date): Promise<GameInvite | null> {
    for (const i of this.invites.values()) {
      if (i.senderId === sender && i.status === "pending" && i.expiresAt > now) return Promise.resolve(i);
    }
    return Promise.resolve(null);
  }
  lastInviteCreatedAt(sender: PlayerId): Promise<Date | null> {
    let latest: Date | null = null;
    for (const i of this.invites.values()) {
      if (i.senderId === sender && (!latest || i.createdAt > latest)) latest = i.createdAt;
    }
    return Promise.resolve(latest);
  }

  saveMatch(record: MatchRecord, results: readonly MatchResult[]): Promise<void> {
    this.matches.push({ record, results });
    return Promise.resolve();
  }
  /** Test yardımcısı. */
  savedMatches(): readonly { record: MatchRecord; results: readonly MatchResult[] }[] {
    return this.matches;
  }

  addCoins(id: PlayerId, delta: number, reason: string, refId: string | null): Promise<number> {
    const p = this.profiles.get(id);
    if (!p) return Promise.resolve(0);
    const coins = Math.max(0, p.coins + delta);
    this.profiles.set(id, { ...p, coins });
    this.coinLedger.push({ id, delta, reason, refId });
    return Promise.resolve(coins);
  }
  spendCoins(id: PlayerId, amount: number, reason: string, refId: string | null): Promise<number | null> {
    const p = this.profiles.get(id);
    if (!p || amount < 0 || p.coins < amount) return Promise.resolve(null);
    const coins = p.coins - amount;
    this.profiles.set(id, { ...p, coins });
    this.coinLedger.push({ id, delta: -amount, reason, refId });
    return Promise.resolve(coins);
  }
  withPlayerLock<T>(id: PlayerId, fn: () => Promise<T>): Promise<T> {
    return this.locks.run(id, fn);
  }
  addStars(id: PlayerId, stars: number, at: Date): Promise<number> {
    const p = this.profiles.get(id);
    if (!p) return Promise.resolve(0);
    const totalStars = p.totalStars + stars;
    this.profiles.set(id, { ...p, totalStars });
    this.starEvents.push({ id, stars, at });
    return Promise.resolve(totalStars);
  }
  weeklyStars(ids: readonly PlayerId[], since: Date): Promise<ReadonlyMap<PlayerId, number>> {
    const out = new Map<PlayerId, number>(ids.map((i) => [i, 0]));
    for (const e of this.starEvents) {
      if (e.at >= since && out.has(e.id)) out.set(e.id, (out.get(e.id) ?? 0) + e.stars);
    }
    return Promise.resolve(out);
  }
  grantInventory(id: PlayerId, rewardId: string): Promise<boolean> {
    let inv = this.inventory.get(id);
    if (!inv) {
      inv = new Map();
      this.inventory.set(id, inv);
    }
    if (inv.has(rewardId)) return Promise.resolve(false);
    inv.set(rewardId, false);
    return Promise.resolve(true);
  }
  listInventory(id: PlayerId): Promise<readonly { rewardId: string; equipped: boolean }[]> {
    return Promise.resolve([...(this.inventory.get(id) ?? new Map<string, boolean>())].map(([rewardId, equipped]) => ({ rewardId, equipped })));
  }
  setEquipped(id: PlayerId, rewardId: string, equipped: boolean): Promise<void> {
    const inv = this.inventory.get(id);
    if (inv?.has(rewardId)) inv.set(rewardId, equipped);
    return Promise.resolve();
  }

  getProgress(id: PlayerId, levelId: number): Promise<LevelProgress | null> {
    return Promise.resolve(this.progress.get(`${id}:${levelId}`) ?? null);
  }
  upsertProgress(progress: LevelProgress): Promise<void> {
    this.progress.set(`${progress.playerId}:${progress.levelId}`, progress);
    return Promise.resolve();
  }
  listProgress(id: PlayerId): Promise<readonly LevelProgress[]> {
    return Promise.resolve([...this.progress.values()].filter((p) => p.playerId === id));
  }

  getDailyClaim(id: PlayerId): Promise<DailyClaimState | null> {
    return Promise.resolve(this.daily.get(id) ?? null);
  }
  putDailyClaim(id: PlayerId, state: DailyClaimState): Promise<void> {
    this.daily.set(id, state);
    return Promise.resolve();
  }

  getSave(id: PlayerId): Promise<{ data: unknown; updatedAt: Date } | null> {
    return Promise.resolve(this.saves.get(id) ?? null);
  }
  putSave(id: PlayerId, data: unknown, at: Date): Promise<void> {
    this.saves.set(id, { data, updatedAt: at });
    return Promise.resolve();
  }
}
