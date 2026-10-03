using System.Collections.Generic;

namespace MinikDuello.Services.Save
{
    public sealed class LevelRecord
    {
        public int Stars { get; set; }
        public int BestScore { get; set; }
    }

    public sealed class PendingResult
    {
        public int LevelId { get; set; }
        public int Score { get; set; }
        public int DurationMs { get; set; }
    }

    /// <summary>
    /// Yerel kayıt. Yıldız/coin/envanter sunucu otoriterdir; buradaki değerler yalnızca çevrimdışı gösterim önbelleğidir.
    /// Gönderilmemiş bölüm sonuçları sırayla kuyrukta bekler.
    /// </summary>
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        public int Version { get; set; } = CurrentVersion;
        public Dictionary<int, LevelRecord> Levels { get; set; } = new Dictionary<int, LevelRecord>();
        public List<PendingResult> Pending { get; set; } = new List<PendingResult>();
        public Dictionary<int, int> StruggleStreaks { get; set; } = new Dictionary<int, int>();

        public int MusicVolume { get; set; } = 70;
        public int SfxVolume { get; set; } = 80;
        public bool VoiceEnabled { get; set; } = true;

        /// <summary>Gün (yyyy-MM-dd) → oynanan dakika. Ebeveyn süre sınırı için.</summary>
        public Dictionary<string, double> PlayMinutes { get; set; } = new Dictionary<string, double>();

        public string CachedUsername { get; set; }
        public string CachedFriendCode { get; set; }
        public string CachedCharacter { get; set; } = "panda";
        public int CachedStars { get; set; }
        public int CachedCoins { get; set; }
        public int CachedLevel { get; set; } = 1;
        public List<string> OwnedRewards { get; set; } = new List<string>();
        public List<string> EquippedRewards { get; set; } = new List<string>();
        /// <summary>Çevrimdışıyken de uygulanabilmesi için ebeveyn ayarlarının önbelleği (yalnızca kısıtlayıcı olanlar).</summary>
        public int CachedDailyLimit { get; set; }
        public bool CachedSoundEnabled { get; set; } = true;
        public string LastDailyPromptDay { get; set; }
        public bool OnboardingDone { get; set; }
    }
}
