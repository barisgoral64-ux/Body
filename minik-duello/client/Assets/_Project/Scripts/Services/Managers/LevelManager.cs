using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MinikDuello.Core;
using MinikDuello.Domain.Levels;
using MinikDuello.Domain.Net;
using MinikDuello.Services.Api;
using MinikDuello.Services.Save;

namespace MinikDuello.Services.Managers
{
    public sealed class LevelCompletion
    {
        public int Stars;
        public int StarsGainedLocal;
        public bool NextUnlocked;
        public int? NextLevelId;
        /// <summary>Sunucu yanıtı (çevrimdışıysa null: ödüller bağlanınca işlenir).</summary>
        public LevelResultDto Server;
    }

    /// <summary>
    /// Bölüm ilerlemesi. Çevrimdışı-öncelikli: sonuç yerelde hemen kaydedilir, sunucuya sırayla gönderilir.
    /// Sunucu yıldız/coin/kilit kararını yeniden doğrular.
    /// </summary>
    public sealed class LevelManager
    {
        private readonly LevelCatalog catalog;
        private readonly SaveManager save;
        private readonly ApiClient api;
        private readonly DifficultyManager difficulty;
        private bool flushing;

        public LevelManager(LevelCatalog catalog, SaveManager save, ApiClient api, DifficultyManager difficulty)
        {
            this.catalog = catalog;
            this.save = save;
            this.api = api;
            this.difficulty = difficulty;
        }

        public LevelCatalog Catalog => catalog;
        public int PendingCount => save.Data.Pending.Count;

        public int Stars(int levelId) => save.Data.Levels.TryGetValue(levelId, out LevelRecord r) ? r.Stars : 0;
        public int BestScore(int levelId) => save.Data.Levels.TryGetValue(levelId, out LevelRecord r) ? r.BestScore : 0;

        public bool IsUnlocked(int levelId)
        {
            if (!catalog.Exists(levelId)) return false;
            return levelId == 1 || Stars(levelId - 1) >= 1;
        }

        public int TotalStars()
        {
            int sum = 0;
            foreach (LevelRecord r in save.Data.Levels.Values) sum += r.Stars;
            return sum;
        }

        public int WorldStars(int worldId)
        {
            int sum = 0;
            foreach (LevelConfig l in catalog.InWorld(worldId)) sum += Stars(l.LevelId);
            return sum;
        }

        public bool IsWorldUnlocked(int worldId, int levelsPerWorld) => IsUnlocked((worldId - 1) * levelsPerWorld + 1);

        /// <summary>Oynanacak bir sonraki bölüm (ilk 3 yıldızsız açık bölüm; hepsi bittiyse son bölüm).</summary>
        public int NextRecommended()
        {
            for (int i = 1; i <= catalog.Count; i++)
            {
                if (IsUnlocked(i) && Stars(i) == 0) return i;
            }
            return catalog.Count;
        }

        public DifficultyManager.Plan PlanFor(int levelId)
        {
            int streak = save.Data.StruggleStreaks.TryGetValue(levelId, out int s) ? s : 0;
            return difficulty.Adjust(catalog.Get(levelId), streak);
        }

        /// <summary>
        /// Sonucu yerelde HEMEN kaydeder ve kuyruğa ekler (ağ beklenmez; sonuç ekranı anında açılabilir).
        /// Sunucuya gönderim FlushPendingAsync ile ayrıca yapılır.
        /// </summary>
        public Result<LevelCompletion> CompleteLocal(int levelId, int score, int durationMs)
        {
            if (!catalog.Exists(levelId)) return Result<LevelCompletion>.Fail(ErrorCode.NotFound, "Bölüm yok");
            if (!IsUnlocked(levelId)) return Result<LevelCompletion>.Fail(ErrorCode.Forbidden, "Bölüm kilitli");

            LevelConfig level = catalog.Get(levelId);
            int clamped = Math.Max(0, Math.Min(level.TargetScore, score));
            int stars = StarCalculator.StarsForScore(level, clamped);
            int previous = Stars(levelId);

            save.Update(d =>
            {
                d.Levels.TryGetValue(levelId, out LevelRecord record);
                if (record == null) record = new LevelRecord();
                record.Stars = Math.Max(record.Stars, stars);
                record.BestScore = Math.Max(record.BestScore, clamped);
                d.Levels[levelId] = record;

                d.StruggleStreaks.TryGetValue(levelId, out int streak);
                d.StruggleStreaks[levelId] = DifficultyManager.NextStreak(streak, stars);
                d.Pending.Add(new PendingResult { LevelId = levelId, Score = clamped, DurationMs = durationMs });
            });

            return Result<LevelCompletion>.Ok(new LevelCompletion
            {
                Stars = stars,
                StarsGainedLocal = Math.Max(0, stars - previous),
                NextUnlocked = stars >= 1 && level.NextLevelId.HasValue,
                NextLevelId = stars >= 1 ? level.NextLevelId : null
            });
        }

        /// <summary>Yerel kayıt + sunucuya gönderim denemesi (testler ve basit akışlar için).</summary>
        public async Task<Result<LevelCompletion>> CompleteAsync(int levelId, int score, int durationMs)
        {
            Result<LevelCompletion> local = CompleteLocal(levelId, score, durationMs);
            if (!local.IsOk) return local;
            local.Value.Server = await FlushPendingAsync(levelId);
            return local;
        }

        /// <summary>
        /// Bekleyen sonuçları sırayla gönderir. Ağ yoksa durur (sonra tekrar denenir); kalıcı reddedilenler atılır.
        /// Verilen bölüm için son sunucu yanıtını döndürür.
        /// </summary>
        public async Task<LevelResultDto> FlushPendingAsync(int reportLevelId = 0)
        {
            if (flushing) return null;
            flushing = true;
            LevelResultDto reported = null;
            try
            {
                while (save.Data.Pending.Count > 0)
                {
                    PendingResult item = save.Data.Pending[0];
                    Result<LevelResultDto> result = await api.PostAsync<LevelResultDto>("/v1/progress/level-result",
                        new LevelResultRequestDto { LevelId = item.LevelId, Score = item.Score, DurationMs = item.DurationMs });

                    if (result.IsOk)
                    {
                        if (item.LevelId == reportLevelId) reported = result.Value;
                    }
                    else if (result.Error == ErrorCode.Network || result.Error == ErrorCode.Unauthorized || result.Error == ErrorCode.RateLimited
                             || result.Error == ErrorCode.Internal)
                    {
                        break; // geçici: sırayı koruyarak sonra tekrar dene
                    }
                    else
                    {
                        Log.Warn("Levels", "Sunucu sonucu reddetti, atılıyor: bölüm " + item.LevelId + " (" + result.Error + ")");
                    }
                    save.Update(d => d.Pending.RemoveAt(0));
                }
            }
            finally
            {
                flushing = false;
            }
            return reported;
        }

        /// <summary>Sunucu ilerlemesini yerelle birleştirir (her bölüm için en yüksek değer).</summary>
        public async Task<bool> SyncFromServerAsync()
        {
            Result<ProgressDto> result = await api.GetAsync<ProgressDto>("/v1/progress");
            if (!result.IsOk) return false;
            save.Update(d =>
            {
                foreach (LevelProgressDto p in result.Value.Levels)
                {
                    if (p.Stars <= 0 && p.BestScore <= 0) continue;
                    d.Levels.TryGetValue(p.LevelId, out LevelRecord local);
                    if (local == null) local = new LevelRecord();
                    local.Stars = Math.Max(local.Stars, p.Stars);
                    local.BestScore = Math.Max(local.BestScore, p.BestScore);
                    d.Levels[p.LevelId] = local;
                }
            });
            return true;
        }
    }
}
