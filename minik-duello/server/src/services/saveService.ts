import { SAVE } from "../config/constants.js";
import type { PlayerId } from "../domain/models.js";
import type { Clock } from "../infra/runtime.js";
import type { DataStore } from "../infra/store.js";
import { ErrorCode, fail, ok, type Result } from "../shared/result.js";

/**
 * Bulut kaydı yalnızca istemci tercihlerini (ayarlar, eğitim ipuçları vb.) taşır.
 * Yıldız, coin, envanter ve bölüm ilerlemesi sunucu otoriterdir; kayıttan ASLA okunmaz.
 */
export class SaveService {
  constructor(
    private readonly store: DataStore,
    private readonly clock: Clock,
  ) {}

  async load(playerId: PlayerId): Promise<Result<{ data: unknown; updatedAt: Date } | null>> {
    return ok(await this.store.getSave(playerId));
  }

  async save(playerId: PlayerId, data: unknown): Promise<Result<{ updatedAt: Date }>> {
    const json = JSON.stringify(data);
    if (json === undefined || Buffer.byteLength(json, "utf8") > SAVE.maxBytes) {
      return fail(ErrorCode.InvalidInput, "Kayıt çok büyük");
    }
    const at = this.clock.now();
    await this.store.putSave(playerId, data, at);
    return ok({ updatedAt: at });
  }
}
