import type { CharacterId, PlayerId } from "../domain/models.js";
import type { Clock } from "../infra/runtime.js";
import type { DataStore } from "../infra/store.js";
import { ErrorCode, fail, ok, type Result } from "../shared/result.js";

export interface WeeklyEntry {
  readonly playerId: PlayerId;
  readonly username: string;
  readonly avatarCharacter: CharacterId;
  readonly stars: number;
  readonly isSelf: boolean;
}

const DAYS_PER_WEEK = 7;
const MS_PER_DAY = 24 * 60 * 60 * 1000;

/** Haftanın başı: Pazartesi 00:00 UTC. */
export function startOfWeek(now: Date): Date {
  const day = (now.getUTCDay() + DAYS_PER_WEEK - 1) % DAYS_PER_WEEK;
  const midnight = Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate());
  return new Date(midnight - day * MS_PER_DAY);
}

/** Yalnızca arkadaşlar arası haftalık yıldız; global sıralama yok. */
export class LeaderboardService {
  constructor(
    private readonly store: DataStore,
    private readonly clock: Clock,
  ) {}

  async weeklyAmongFriends(playerId: PlayerId): Promise<Result<readonly WeeklyEntry[]>> {
    const settings = await this.store.getSettings(playerId);
    if (!settings?.friendsEnabled) return fail(ErrorCode.FriendsDisabled, "Arkadaş sistemi kapalı");

    const ids: PlayerId[] = [playerId];
    for (const f of await this.store.listFriendIds(playerId)) {
      if (!(await this.store.isBlockedEitherWay(playerId, f))) ids.push(f);
    }
    const stars = await this.store.weeklyStars(ids, startOfWeek(this.clock.now()));

    const entries: WeeklyEntry[] = [];
    for (const id of ids) {
      const [player, profile] = await Promise.all([this.store.getPlayer(id), this.store.getProfile(id)]);
      if (player && profile) {
        entries.push({
          playerId: id, username: player.username, avatarCharacter: profile.avatarCharacter,
          stars: stars.get(id) ?? 0, isSelf: id === playerId,
        });
      }
    }
    entries.sort((a, b) => b.stars - a.stars || a.username.localeCompare(b.username));
    return ok(entries);
  }
}
