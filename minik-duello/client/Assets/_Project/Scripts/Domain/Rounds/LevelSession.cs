using System;
using System.Collections.Generic;
using MinikDuello.Domain.Levels;

namespace MinikDuello.Domain.Rounds
{
    /// <summary>Bir bölüm oynanışının durumu: turlar, puan, hata sayısı. UI'dan bağımsızdır.</summary>
    public sealed class LevelSession
    {
        private readonly List<RoundSpec> rounds;

        public LevelConfig Level { get; }
        public int Index { get; private set; }
        public int Score { get; private set; }
        public int TotalMistakes { get; private set; }
        public double StartSeconds { get; }

        public LevelSession(LevelConfig level, IEnumerable<RoundSpec> rounds, double nowSeconds)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
            this.rounds = new List<RoundSpec>(rounds ?? throw new ArgumentNullException(nameof(rounds)));
            if (this.rounds.Count == 0) throw new ArgumentException("En az bir tur gerekli.", nameof(rounds));
            StartSeconds = nowSeconds;
        }

        public int RoundCount => rounds.Count;
        public bool IsFinished => Index >= rounds.Count;
        public RoundSpec Current => IsFinished ? null : rounds[Index];

        /// <summary>Turu bitirir, kazanılan puanı döndürür (hata puanı kırar, sıfırlamaz).</summary>
        public int CompleteRound(int mistakes)
        {
            if (IsFinished) throw new InvalidOperationException("Bölüm zaten bitti.");
            int points = ScoreCalculator.RoundPoints(mistakes);
            Score += points;
            TotalMistakes += Math.Max(0, mistakes);
            Index++;
            return points;
        }

        public int ElapsedMs(double nowSeconds) => (int)Math.Max(0d, (nowSeconds - StartSeconds) * 1000d);
    }
}
