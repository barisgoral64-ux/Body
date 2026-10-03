namespace MinikDuello.Domain.Rewards
{
    public sealed class DailyEntry
    {
        public int Coins;
        public string RewardId;
    }

    /// <summary>Günlük ödül takvimi (server/src/config/constants.ts ile aynı). Kaçırılan gün seriyi bozmaz.</summary>
    public static class DailyCycle
    {
        public static readonly DailyEntry[] Days =
        {
            new DailyEntry { Coins = 50 },
            new DailyEntry { RewardId = "sticker_star" },
            new DailyEntry { Coins = 100 },
            new DailyEntry { RewardId = "sticker_heart" },
            new DailyEntry { Coins = 150 },
            new DailyEntry { RewardId = "sticker_rainbow" },
            new DailyEntry { Coins = 200 }
        };
    }
}
