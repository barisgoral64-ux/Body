import { PRESENCE } from "../config/constants.js";
import type { PlayerId, PresenceStatus } from "../domain/models.js";
import type { Clock } from "../infra/runtime.js";
import type { DataStore } from "../infra/store.js";

interface Entry {
  status: Exclude<PresenceStatus, "offline">;
  lastBeat: number;
}

/**
 * Çevrimiçi durum. Gizlilik kuralı: izleyici yalnızca arkadaşı ve ebeveyn izinleri açıksa gerçek durumu görür;
 * diğer tüm durumlarda "offline" döner (gizlenmiş ile gerçekten çevrimdışı ayırt edilemez).
 */
export class PresenceService {
  private readonly entries = new Map<PlayerId, Entry>();

  constructor(
    private readonly store: DataStore,
    private readonly clock: Clock,
  ) {}

  set(playerId: PlayerId, status: Exclude<PresenceStatus, "offline">): void {
    this.entries.set(playerId, { status, lastBeat: this.clock.now().getTime() });
  }

  heartbeat(playerId: PlayerId): void {
    const e = this.entries.get(playerId);
    if (e) e.lastBeat = this.clock.now().getTime();
  }

  clear(playerId: PlayerId): void {
    this.entries.delete(playerId);
  }

  /** Gerçek (gizlilik uygulanmamış) durum: yalnızca sunucu içi kullanım. */
  actual(playerId: PlayerId): PresenceStatus {
    const e = this.entries.get(playerId);
    if (!e) return "offline";
    return this.clock.now().getTime() - e.lastBeat > PRESENCE.ttlMs ? "offline" : e.status;
  }

  async visibleTo(viewerId: PlayerId, targetId: PlayerId): Promise<PresenceStatus> {
    const status = this.actual(targetId);
    if (status === "offline") return "offline";
    const [viewer, target] = await Promise.all([this.store.getSettings(viewerId), this.store.getSettings(targetId)]);
    if (!viewer?.friendsEnabled || !target?.friendsEnabled || !target.onlineStatusVisible) return "offline";
    if (!(await this.store.areFriends(viewerId, targetId))) return "offline";
    if (await this.store.isBlockedEitherWay(viewerId, targetId)) return "offline";
    return status;
  }
}
