import { REWARDS } from "../config/constants.js";
import { generateFriendCode } from "../domain/identity.js";
import type { CharacterId, PlayerId } from "../domain/models.js";
import type { DataStore } from "../infra/store.js";
import { ErrorCode, fail, ok, type Result } from "../shared/result.js";

export interface PlayerView {
  readonly playerId: PlayerId;
  readonly username: string;
  readonly friendCode: string;
  readonly avatarCharacter: CharacterId;
  readonly avatarFrame: string | null;
  readonly level: number;
  readonly totalStars: number;
  readonly coins: number;
}

/** Başkalarına gösterilen kısıtlı görünüm: arkadaş kodu ve bakiye yok. */
export interface PublicPlayerView {
  readonly playerId: PlayerId;
  readonly username: string;
  readonly avatarCharacter: CharacterId;
  readonly avatarFrame: string | null;
}

const MAX_CODE_ATTEMPTS = 50;

export function levelForStars(totalStars: number): number {
  return 1 + Math.floor(totalStars / REWARDS.starsPerPlayerLevel);
}

export class PlayerService {
  constructor(private readonly store: DataStore) {}

  async getSelf(playerId: PlayerId): Promise<Result<PlayerView>> {
    const [player, profile] = await Promise.all([this.store.getPlayer(playerId), this.store.getProfile(playerId)]);
    if (!player || !profile) return fail(ErrorCode.NotFound, "Oyuncu bulunamadı");
    return ok({
      playerId,
      username: player.username,
      friendCode: player.friendCode,
      avatarCharacter: profile.avatarCharacter,
      avatarFrame: profile.avatarFrame,
      level: profile.level,
      totalStars: profile.totalStars,
      coins: profile.coins,
    });
  }

  async getPublic(playerId: PlayerId): Promise<PublicPlayerView | null> {
    const [player, profile] = await Promise.all([this.store.getPlayer(playerId), this.store.getProfile(playerId)]);
    if (!player || !profile) return null;
    return {
      playerId,
      username: player.username,
      avatarCharacter: profile.avatarCharacter,
      avatarFrame: profile.avatarFrame,
    };
  }

  async regenerateFriendCode(playerId: PlayerId): Promise<Result<string>> {
    for (let i = 0; i < MAX_CODE_ATTEMPTS; i += 1) {
      const code = generateFriendCode();
      if (await this.store.setFriendCode(playerId, code)) return ok(code);
    }
    return fail(ErrorCode.Internal, "Arkadaş kodu üretilemedi");
  }
}
