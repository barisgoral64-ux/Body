using System;

namespace MinikDuello.Domain.Rounds
{
    /// <summary>
    /// Tek oyunculu tur puanı. Hata puan kırar ama ASLA sıfırlamaz/negatif yapmaz:
    /// çocuk her bölümü bitirdiğinde en az 1 yıldız kazanır (kaybetme yok).
    /// </summary>
    public static class ScoreCalculator
    {
        public const int FullPoints = 100;
        public const int MistakePenalty = 20;
        public const int MinPoints = 50;

        public static int RoundPoints(int mistakes)
        {
            int points = FullPoints - Math.Max(0, mistakes) * MistakePenalty;
            return Math.Max(MinPoints, points);
        }
    }
}
