import { Pool, type PoolClient, type PoolConfig } from "pg";
import { orderFriendPair } from "../domain/friendRequest.js";
import { KeyedMutex } from "./mutex.js";
import type {
  CharacterId, FriendRequest, FriendRequestStatus, GameInvite, GameMode, InviteStatus, MatchResult, Player,
  PlayerId, PlayerProfile,
} from "../domain/models.js";
import type {
  DailyClaimState, DataStore, LevelProgress, MatchRecord, ProfilePatch, StoredParentSettings,
} from "./store.js";

const UNIQUE_VIOLATION = "23505";

type Row = Record<string, unknown>;

const toPlayer = (r: Row): Player => ({
  playerId: r.player_id as string,
  friendCode: r.friend_code as string,
  username: r.username as string,
  createdAt: r.created_at as Date,
  lastSeenAt: r.last_seen_at as Date,
});

const toProfile = (r: Row): PlayerProfile => ({
  playerId: r.player_id as string,
  avatarCharacter: r.avatar_character as CharacterId,
  avatarFrame: (r.avatar_frame as string | null) ?? null,
  level: r.level as number,
  totalStars: r.total_stars as number,
  coins: r.coins as number,
});

const toSettings = (r: Row): StoredParentSettings => ({
  playerId: r.player_id as string,
  friendsEnabled: r.friends_enabled as boolean,
  multiplayerEnabled: r.multiplayer_enabled as boolean,
  onlineStatusVisible: r.online_status_visible as boolean,
  gameInvitationsEnabled: r.game_invitations_enabled as boolean,
  dailyLimitMinutes: r.daily_limit_minutes as number,
  soundEnabled: r.sound_enabled as boolean,
  pinHash: (r.pin_hash as string | null) ?? null,
  pinFailedAttempts: r.pin_failed_attempts as number,
  pinLockedUntil: (r.pin_locked_until as Date | null) ?? null,
  pinLockoutCount: (r.pin_lockout_count as number | undefined) ?? 0,
});

const toRequest = (r: Row): FriendRequest => ({
  requestId: r.request_id as string,
  senderPlayerId: r.sender_id as string,
  receiverPlayerId: r.receiver_id as string,
  status: r.status as FriendRequestStatus,
  createdAt: r.created_at as Date,
  expiresAt: r.expires_at as Date,
});

const toInvite = (r: Row): GameInvite => ({
  inviteId: r.invite_id as string,
  senderId: r.sender_id as string,
  receiverId: r.receiver_id as string,
  gameMode: r.game_mode as GameMode,
  createdAt: r.created_at as Date,
  expiresAt: r.expires_at as Date,
  status: r.status as InviteStatus,
});

/** PostgreSQL DataStore. Çok adımlı yazmalar tek işlemde yapılır; çift ödül/bakiye bozulması olmaz. */
export class PgStore implements DataStore {
  private readonly localLocks = new KeyedMutex();

  /**
   * @param lockPool Danışma kilitleri için ayrı havuz: kilit bekleyen bağlantılar ana havuzu tüketip
   *                 kilidi tutan işlemin iç sorgularını aç bırakamaz (kilitlenme önlemi).
   */
  constructor(
    private readonly pool: Pool,
    private readonly lockPool: Pool = pool,
  ) {}

  static connect(config: PoolConfig): PgStore {
    return new PgStore(new Pool(config), new Pool(config));
  }

  async close(): Promise<void> {
    await this.pool.end();
    if (this.lockPool !== this.pool) await this.lockPool.end();
  }

  private async tx<T>(fn: (client: PoolClient) => Promise<T>): Promise<T> {
    const client = await this.pool.connect();
    try {
      await client.query("BEGIN");
      const result = await fn(client);
      await client.query("COMMIT");
      return result;
    } catch (error) {
      await client.query("ROLLBACK");
      throw error;
    } finally {
      client.release();
    }
  }

  private async one(text: string, values: unknown[]): Promise<Row | null> {
    return (await this.pool.query(text, values)).rows[0] ?? null;
  }

  async createPlayer(player: Player, deviceHash: string, profile: PlayerProfile, s: StoredParentSettings): Promise<void> {
    await this.tx(async (c) => {
      await c.query(
        "INSERT INTO players(player_id, device_hash, friend_code, username, created_at, last_seen_at) VALUES ($1,$2,$3,$4,$5,$6)",
        [player.playerId, deviceHash, player.friendCode, player.username, player.createdAt, player.lastSeenAt],
      );
      await c.query(
        "INSERT INTO player_profiles(player_id, avatar_character, avatar_frame, level, total_stars, coins) VALUES ($1,$2,$3,$4,$5,$6)",
        [profile.playerId, profile.avatarCharacter, profile.avatarFrame, profile.level, profile.totalStars, profile.coins],
      );
      await c.query(
        `INSERT INTO parent_settings(player_id, friends_enabled, multiplayer_enabled, online_status_visible,
           game_invitations_enabled, daily_limit_minutes, sound_enabled, pin_hash, pin_failed_attempts, pin_locked_until, pin_lockout_count)
         VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)`,
        [s.playerId, s.friendsEnabled, s.multiplayerEnabled, s.onlineStatusVisible, s.gameInvitationsEnabled,
          s.dailyLimitMinutes, s.soundEnabled, s.pinHash, s.pinFailedAttempts, s.pinLockedUntil, s.pinLockoutCount],
      );
    });
  }

  async getPlayer(id: PlayerId): Promise<Player | null> {
    const r = await this.one("SELECT player_id, friend_code, username, created_at, last_seen_at FROM players WHERE player_id=$1", [id]);
    return r ? toPlayer(r) : null;
  }
  async getPlayerByDeviceHash(hash: string): Promise<Player | null> {
    const r = await this.one("SELECT player_id, friend_code, username, created_at, last_seen_at FROM players WHERE device_hash=$1", [hash]);
    return r ? toPlayer(r) : null;
  }
  async getPlayerByFriendCode(code: string): Promise<Player | null> {
    const r = await this.one("SELECT player_id, friend_code, username, created_at, last_seen_at FROM players WHERE friend_code=$1", [code]);
    return r ? toPlayer(r) : null;
  }
  async setFriendCode(id: PlayerId, code: string): Promise<boolean> {
    try {
      const res = await this.pool.query("UPDATE players SET friend_code=$2 WHERE player_id=$1", [id, code]);
      return (res.rowCount ?? 0) > 0;
    } catch (error) {
      if ((error as { code?: string }).code === UNIQUE_VIOLATION) return false;
      throw error;
    }
  }
  async touchPlayer(id: PlayerId, at: Date): Promise<void> {
    await this.pool.query("UPDATE players SET last_seen_at=$2 WHERE player_id=$1", [id, at]);
  }
  async deletePlayer(id: PlayerId): Promise<void> {
    // Tüm bağlı tablolar ON DELETE CASCADE / SET NULL ile temizlenir (bkz. migrations).
    await this.pool.query("DELETE FROM players WHERE player_id=$1", [id]);
  }
  async getProfile(id: PlayerId): Promise<PlayerProfile | null> {
    const r = await this.one("SELECT * FROM player_profiles WHERE player_id=$1", [id]);
    return r ? toProfile(r) : null;
  }
  async updateProfile(id: PlayerId, patch: ProfilePatch): Promise<void> {
    await this.pool.query(
      `UPDATE player_profiles SET
         avatar_character = COALESCE($2, avatar_character),
         avatar_frame = CASE WHEN $3::boolean THEN $4 ELSE avatar_frame END,
         level = COALESCE($5, level)
       WHERE player_id=$1`,
      [id, patch.avatarCharacter ?? null, "avatarFrame" in patch, patch.avatarFrame ?? null, patch.level ?? null],
    );
  }

  async getSettings(id: PlayerId): Promise<StoredParentSettings | null> {
    const r = await this.one("SELECT * FROM parent_settings WHERE player_id=$1", [id]);
    return r ? toSettings(r) : null;
  }
  async saveSettings(s: StoredParentSettings): Promise<void> {
    await this.pool.query(
      `UPDATE parent_settings SET friends_enabled=$2, multiplayer_enabled=$3, online_status_visible=$4,
         game_invitations_enabled=$5, daily_limit_minutes=$6, sound_enabled=$7, pin_hash=$8,
         pin_failed_attempts=$9, pin_locked_until=$10, pin_lockout_count=$11 WHERE player_id=$1`,
      [s.playerId, s.friendsEnabled, s.multiplayerEnabled, s.onlineStatusVisible, s.gameInvitationsEnabled,
        s.dailyLimitMinutes, s.soundEnabled, s.pinHash, s.pinFailedAttempts, s.pinLockedUntil, s.pinLockoutCount],
    );
  }

  async addFriendPair(a: PlayerId, b: PlayerId, at: Date): Promise<void> {
    const [x, y] = orderFriendPair(a, b);
    await this.pool.query("INSERT INTO friends(player_a, player_b, created_at) VALUES ($1,$2,$3) ON CONFLICT DO NOTHING", [x, y, at]);
  }
  async removeFriendPair(a: PlayerId, b: PlayerId): Promise<void> {
    const [x, y] = orderFriendPair(a, b);
    await this.pool.query("DELETE FROM friends WHERE player_a=$1 AND player_b=$2", [x, y]);
  }
  async areFriends(a: PlayerId, b: PlayerId): Promise<boolean> {
    const [x, y] = orderFriendPair(a, b);
    return (await this.one("SELECT 1 AS x FROM friends WHERE player_a=$1 AND player_b=$2", [x, y])) !== null;
  }
  async listFriendIds(id: PlayerId): Promise<readonly PlayerId[]> {
    const res = await this.pool.query(
      "SELECT CASE WHEN player_a=$1 THEN player_b ELSE player_a END AS friend FROM friends WHERE player_a=$1 OR player_b=$1",
      [id],
    );
    return res.rows.map((r: Row) => r.friend as string);
  }

  async insertFriendRequest(r: FriendRequest): Promise<void> {
    await this.pool.query(
      "INSERT INTO friend_requests(request_id, sender_id, receiver_id, status, created_at, expires_at) VALUES ($1,$2,$3,$4,$5,$6)",
      [r.requestId, r.senderPlayerId, r.receiverPlayerId, r.status, r.createdAt, r.expiresAt],
    );
  }
  async getFriendRequest(id: string): Promise<FriendRequest | null> {
    const r = await this.one("SELECT * FROM friend_requests WHERE request_id=$1", [id]);
    return r ? toRequest(r) : null;
  }
  async setFriendRequestStatus(id: string, status: FriendRequestStatus, at: Date): Promise<void> {
    await this.pool.query("UPDATE friend_requests SET status=$2, resolved_at=$3 WHERE request_id=$1", [id, status, at]);
  }
  async listPendingIncoming(receiver: PlayerId, now: Date): Promise<readonly FriendRequest[]> {
    const res = await this.pool.query(
      "SELECT * FROM friend_requests WHERE receiver_id=$1 AND status='pending' AND expires_at > $2 ORDER BY created_at",
      [receiver, now],
    );
    return res.rows.map(toRequest);
  }
  async findPendingBetween(a: PlayerId, b: PlayerId): Promise<FriendRequest | null> {
    const r = await this.one(
      `SELECT * FROM friend_requests WHERE status='pending'
         AND ((sender_id=$1 AND receiver_id=$2) OR (sender_id=$2 AND receiver_id=$1)) LIMIT 1`,
      [a, b],
    );
    return r ? toRequest(r) : null;
  }
  async lastRejectedAt(sender: PlayerId, receiver: PlayerId): Promise<Date | null> {
    const r = await this.one(
      "SELECT MAX(resolved_at) AS at FROM friend_requests WHERE sender_id=$1 AND receiver_id=$2 AND status='rejected'",
      [sender, receiver],
    );
    return (r?.at as Date | null) ?? null;
  }
  async countRequestsSentSince(sender: PlayerId, since: Date): Promise<number> {
    const r = await this.one("SELECT COUNT(*)::int AS n FROM friend_requests WHERE sender_id=$1 AND created_at >= $2", [sender, since]);
    return (r?.n as number | undefined) ?? 0;
  }
  async logCodeAttempt(sender: PlayerId, at: Date): Promise<void> {
    await this.pool.query("INSERT INTO friend_code_attempts(sender_id, created_at) VALUES ($1,$2)", [sender, at]);
  }
  async countCodeAttemptsSince(sender: PlayerId, since: Date): Promise<number> {
    const r = await this.one("SELECT COUNT(*)::int AS n FROM friend_code_attempts WHERE sender_id=$1 AND created_at >= $2", [sender, since]);
    return (r?.n as number | undefined) ?? 0;
  }
  async expireOverdueRequests(now: Date): Promise<number> {
    const res = await this.pool.query(
      "UPDATE friend_requests SET status='expired', resolved_at=$1 WHERE status='pending' AND expires_at <= $1",
      [now],
    );
    return res.rowCount ?? 0;
  }
  async cancelPendingBetween(a: PlayerId, b: PlayerId, at: Date): Promise<void> {
    await this.pool.query(
      `UPDATE friend_requests SET status='rejected', resolved_at=$3 WHERE status='pending'
         AND ((sender_id=$1 AND receiver_id=$2) OR (sender_id=$2 AND receiver_id=$1))`,
      [a, b, at],
    );
  }

  async addBlock(player: PlayerId, blocked: PlayerId, at: Date): Promise<void> {
    await this.pool.query("INSERT INTO blocked_players(player_id, blocked_id, created_at) VALUES ($1,$2,$3) ON CONFLICT DO NOTHING", [player, blocked, at]);
  }
  async removeBlock(player: PlayerId, blocked: PlayerId): Promise<void> {
    await this.pool.query("DELETE FROM blocked_players WHERE player_id=$1 AND blocked_id=$2", [player, blocked]);
  }
  async isBlockedEitherWay(a: PlayerId, b: PlayerId): Promise<boolean> {
    const r = await this.one(
      "SELECT 1 AS x FROM blocked_players WHERE (player_id=$1 AND blocked_id=$2) OR (player_id=$2 AND blocked_id=$1) LIMIT 1",
      [a, b],
    );
    return r !== null;
  }
  async listBlocked(player: PlayerId): Promise<readonly PlayerId[]> {
    const res = await this.pool.query("SELECT blocked_id FROM blocked_players WHERE player_id=$1", [player]);
    return res.rows.map((r: Row) => r.blocked_id as string);
  }

  async insertInvite(i: GameInvite): Promise<void> {
    await this.pool.query(
      "INSERT INTO game_invites(invite_id, sender_id, receiver_id, game_mode, status, created_at, expires_at) VALUES ($1,$2,$3,$4,$5,$6,$7)",
      [i.inviteId, i.senderId, i.receiverId, i.gameMode, i.status, i.createdAt, i.expiresAt],
    );
  }
  async getInvite(id: string): Promise<GameInvite | null> {
    const r = await this.one("SELECT * FROM game_invites WHERE invite_id=$1", [id]);
    return r ? toInvite(r) : null;
  }
  async setInviteStatus(id: string, status: InviteStatus): Promise<void> {
    await this.pool.query("UPDATE game_invites SET status=$2 WHERE invite_id=$1", [id, status]);
  }
  async findPendingInviteFrom(sender: PlayerId, now: Date): Promise<GameInvite | null> {
    const r = await this.one("SELECT * FROM game_invites WHERE sender_id=$1 AND status='pending' AND expires_at > $2 LIMIT 1", [sender, now]);
    return r ? toInvite(r) : null;
  }
  async lastInviteCreatedAt(sender: PlayerId): Promise<Date | null> {
    const r = await this.one("SELECT MAX(created_at) AS at FROM game_invites WHERE sender_id=$1", [sender]);
    return (r?.at as Date | null) ?? null;
  }

  async saveMatch(record: MatchRecord, results: readonly MatchResult[]): Promise<void> {
    const [first, second] = results;
    if (!first || !second) throw new Error("Maç sonucu iki oyuncu içermeli");
    await this.tx(async (c) => {
      await c.query(
        `INSERT INTO game_rooms(room_id, player1_id, player2_id, game_mode, state, created_at)
         VALUES ($1,$2,$3,$4,'finished',$5) ON CONFLICT DO NOTHING`,
        [record.roomId, first.playerId, second.playerId, record.gameMode, record.startedAt],
      );
      await c.query(
        "INSERT INTO matches(match_id, room_id, game_mode, started_at, ended_at, end_reason) VALUES ($1,$2,$3,$4,$5,$6) ON CONFLICT DO NOTHING",
        [record.matchId, record.roomId, record.gameMode, record.startedAt, record.endedAt, record.endReason],
      );
      for (const r of results) {
        await c.query(
          `INSERT INTO match_results(match_id, player_id, score, is_winner, stars_awarded, coins_awarded)
           VALUES ($1,$2,$3,$4,$5,$6) ON CONFLICT DO NOTHING`,
          [record.matchId, r.playerId, r.score, r.isWinner, r.starsAwarded, r.coinsAwarded],
        );
      }
    });
  }

  async addCoins(id: PlayerId, delta: number, reason: string, refId: string | null): Promise<number> {
    return this.tx(async (c) => {
      const res = await c.query("UPDATE player_profiles SET coins = GREATEST(0, coins + $2) WHERE player_id=$1 RETURNING coins", [id, delta]);
      await c.query("INSERT INTO coin_ledger(player_id, delta, reason, ref_id) VALUES ($1,$2,$3,$4)", [id, delta, reason, refId]);
      return (res.rows[0]?.coins as number | undefined) ?? 0;
    });
  }
  async spendCoins(id: PlayerId, amount: number, reason: string, refId: string | null): Promise<number | null> {
    if (amount < 0) return null;
    return this.tx(async (c) => {
      const res = await c.query("UPDATE player_profiles SET coins = coins - $2 WHERE player_id=$1 AND coins >= $2 RETURNING coins", [id, amount]);
      if (res.rowCount === 0) return null;
      await c.query("INSERT INTO coin_ledger(player_id, delta, reason, ref_id) VALUES ($1,$2,$3,$4)", [id, -amount, reason, refId]);
      return res.rows[0]?.coins as number;
    });
  }
  /** Oturum düzeyinde danışma kilidi: birden çok sunucu örneğinde de çift işlemi önler. */
  withPlayerLock<T>(id: PlayerId, fn: () => Promise<T>): Promise<T> {
    // Önce süreç içinde sıraya girilir: aynı oyuncu için DB'de bloklanan bağlantı birikmez.
    return this.localLocks.run(id, async () => {
      const client = await this.lockPool.connect();
      try {
        await client.query("SELECT pg_advisory_lock(hashtext($1))", [id]);
        try {
          return await fn();
        } finally {
          await client.query("SELECT pg_advisory_unlock(hashtext($1))", [id]);
        }
      } finally {
        client.release();
      }
    });
  }
  async addStars(id: PlayerId, stars: number, at: Date): Promise<number> {
    return this.tx(async (c) => {
      const res = await c.query("UPDATE player_profiles SET total_stars = total_stars + $2 WHERE player_id=$1 RETURNING total_stars", [id, stars]);
      await c.query("INSERT INTO star_events(player_id, stars, created_at) VALUES ($1,$2,$3)", [id, stars, at]);
      return (res.rows[0]?.total_stars as number | undefined) ?? 0;
    });
  }
  async weeklyStars(ids: readonly PlayerId[], since: Date): Promise<ReadonlyMap<PlayerId, number>> {
    const out = new Map<PlayerId, number>(ids.map((i) => [i, 0]));
    const res = await this.pool.query(
      "SELECT player_id, SUM(stars)::int AS stars FROM star_events WHERE player_id = ANY($1::uuid[]) AND created_at >= $2 GROUP BY player_id",
      [ids, since],
    );
    for (const r of res.rows as Row[]) out.set(r.player_id as string, r.stars as number);
    return out;
  }
  async grantInventory(id: PlayerId, rewardId: string, at: Date): Promise<boolean> {
    const res = await this.pool.query(
      "INSERT INTO player_inventory(player_id, reward_id, acquired_at) VALUES ($1,$2,$3) ON CONFLICT DO NOTHING",
      [id, rewardId, at],
    );
    return (res.rowCount ?? 0) > 0;
  }
  async listInventory(id: PlayerId): Promise<readonly { rewardId: string; equipped: boolean }[]> {
    const res = await this.pool.query("SELECT reward_id, equipped FROM player_inventory WHERE player_id=$1 ORDER BY acquired_at, reward_id", [id]);
    return res.rows.map((r: Row) => ({ rewardId: r.reward_id as string, equipped: r.equipped as boolean }));
  }
  async setEquipped(id: PlayerId, rewardId: string, equipped: boolean): Promise<void> {
    await this.pool.query("UPDATE player_inventory SET equipped=$3 WHERE player_id=$1 AND reward_id=$2", [id, rewardId, equipped]);
  }

  async getProgress(id: PlayerId, levelId: number): Promise<LevelProgress | null> {
    const r = await this.one("SELECT * FROM player_progress WHERE player_id=$1 AND level_id=$2", [id, levelId]);
    return r ? this.toProgress(r) : null;
  }
  async upsertProgress(p: LevelProgress): Promise<void> {
    await this.pool.query(
      `INSERT INTO player_progress(player_id, level_id, stars, best_score, completed_at) VALUES ($1,$2,$3,$4,$5)
       ON CONFLICT (player_id, level_id) DO UPDATE SET stars=EXCLUDED.stars, best_score=EXCLUDED.best_score, completed_at=EXCLUDED.completed_at`,
      [p.playerId, p.levelId, p.stars, p.bestScore, p.completedAt],
    );
  }
  async listProgress(id: PlayerId): Promise<readonly LevelProgress[]> {
    const res = await this.pool.query("SELECT * FROM player_progress WHERE player_id=$1", [id]);
    return res.rows.map((r: Row) => this.toProgress(r));
  }
  private toProgress(r: Row): LevelProgress {
    return {
      playerId: r.player_id as string, levelId: r.level_id as number, stars: r.stars as number,
      bestScore: r.best_score as number, completedAt: (r.completed_at as Date | null) ?? null,
    };
  }

  async getDailyClaim(id: PlayerId): Promise<DailyClaimState | null> {
    const r = await this.one("SELECT claim_count, to_char(last_claim_day, 'YYYY-MM-DD') AS day FROM daily_claims WHERE player_id=$1", [id]);
    return r ? { claimCount: r.claim_count as number, lastClaimDay: r.day as string } : null;
  }
  async putDailyClaim(id: PlayerId, state: DailyClaimState): Promise<void> {
    await this.pool.query(
      `INSERT INTO daily_claims(player_id, claim_count, last_claim_day) VALUES ($1,$2,$3)
       ON CONFLICT (player_id) DO UPDATE SET claim_count=EXCLUDED.claim_count, last_claim_day=EXCLUDED.last_claim_day`,
      [id, state.claimCount, state.lastClaimDay],
    );
  }

  async getSave(id: PlayerId): Promise<{ data: unknown; updatedAt: Date } | null> {
    const r = await this.one("SELECT data, updated_at FROM player_saves WHERE player_id=$1", [id]);
    return r ? { data: r.data, updatedAt: r.updated_at as Date } : null;
  }
  async putSave(id: PlayerId, data: unknown, at: Date): Promise<void> {
    await this.pool.query(
      `INSERT INTO player_saves(player_id, data, updated_at) VALUES ($1,$2::jsonb,$3)
       ON CONFLICT (player_id) DO UPDATE SET data=EXCLUDED.data, updated_at=EXCLUDED.updated_at`,
      [id, JSON.stringify(data), at],
    );
  }
}
