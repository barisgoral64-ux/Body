namespace MinikDuello.Domain.Levels
{
    public static class StarCalculator
    {
        public const int MaxStars = 3;

        /// <summary>Sunucuyla aynı kural: eşiği geçen her kademe bir yıldız.</summary>
        public static int StarsForScore(LevelConfig level, int score)
        {
            int stars = 0;
            foreach (int threshold in level.StarRequirements)
            {
                if (score >= threshold) stars++;
            }
            return stars > MaxStars ? MaxStars : stars;
        }
    }
}
