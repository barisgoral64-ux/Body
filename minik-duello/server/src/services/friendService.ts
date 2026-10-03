import type { PlayerId, PresenceStatus } from "../domain/models.js";
import type { DataStore } from "../infra/store.js";
import { ErrorCode, fail, ok, type Result } from "../shared/result.js";
import type { PlayerService, PublicPlayerView } from "./playerService.js";
import type { PresenceService } from "./presenceService.js";

export interface FriendView extends PublicPlayerView {
  readonly presence: PresenceStatus;
}

export class FriendService {
  constructor(
    private readonly store: DataStore,
    private readonly players: PlayerService,
    private readonly presence: PresenceService,
  ) {}

  async listFriends(playerId: PlayerId): Promise<Result<readonly FriendView[]>> {
    const settings = await this.store.getSettings(playerId);
    if (!settings?.friendsEnabled) return fail(ErrorCode.FriendsDisabled, "Arkadaş sistemi kapalı");

    const views: FriendView[] = [];
    for (const friendId of await this.store.listFriendIds(playerId)) {
      if (await this.store.isBlockedEitherWay(playerId, friendId)) continue;
      const pub = await this.players.getPublic(friendId);
      if (pub) views.push({ ...pub, presence: await this.presence.visibleTo(playerId, friendId) });
    }
    views.sort((a, b) => a.username.localeCompare(b.username));
    return ok(views);
  }

  areFriends(a: PlayerId, b: PlayerId): Promise<boolean> {
    return this.store.areFriends(a, b);
  }
}
