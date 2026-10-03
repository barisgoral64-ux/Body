using System;

namespace MinikDuello.Domain.Levels
{
    /// <summary>
    /// Adaptif yumuşatma: çocuk aynı bölümde art arda zorlanırsa nesne sayısı geçici azalır ve ipucu açılır.
    /// Zorluk asla katalogdakinden ARTMAZ; başarısızlık mesajı yoktur, yalnızca kolaylaştırma vardır.
    /// </summary>
    public sealed class DifficultyManager
    {
        public const int StruggleThreshold = 2;
        public const int MinObjectCount = 2;
        public const int MaxObjectReduction = 2;
        public const int StruggleStarLimit = 1;

        public sealed class Plan
        {
            public LevelConfig Level;
            public bool HintsEnabled;
            public int Reduction;
        }

        public Plan Adjust(LevelConfig level, int struggleStreak)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            LevelConfig copy = level.Clone();
            int reduction = 0;
            bool hints = false;

            if (struggleStreak >= StruggleThreshold)
            {
                hints = true;
                reduction = Math.Min(MaxObjectReduction, struggleStreak - StruggleThreshold + 1);
                int reduced = Math.Max(MinObjectCount, copy.ObjectCount - reduction);
                reduction = copy.ObjectCount - reduced;
                copy.ObjectCount = reduced;
                // Zaman baskısı yardım modunda kaldırılır.
                copy.TimeLimitSeconds = 0;
            }
            return new Plan { Level = copy, HintsEnabled = hints, Reduction = reduction };
        }

        /// <summary>Sonuç yıldızına göre zorlanma serisini günceller (iyi sonuç seriyi sıfırlar).</summary>
        public static int NextStreak(int currentStreak, int starsEarned)
        {
            return starsEarned <= StruggleStarLimit ? currentStreak + 1 : 0;
        }
    }
}
