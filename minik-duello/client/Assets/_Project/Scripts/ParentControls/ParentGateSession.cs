using System;

namespace MinikDuello.ParentControls
{
    public enum GateResult
    {
        Passed,
        Wrong,
        LockedOut
    }

    /// <summary>Yetişkin kapısı sabitleri (magic number yok).</summary>
    public static class ParentGateRules
    {
        public const int MinLeft = 11;
        public const int MaxLeft = 19;
        public const int MinRight = 6;
        public const int MaxRight = 9;
        public const int MaxAnswerDigits = 3;
        public const int MaxWrongAttempts = 3;
        public const double LockoutSeconds = 30d;
    }

    /// <summary>
    /// Basit toplama sorusu: 4-5 yaş çocuğun kolayca çözemeyeceği (iki basamaklı + tek basamaklı).
    /// Yanlış cevapta yeni soru gelir; art arda yanlışta kısa süre kilitlenir.
    /// Saf C#: Unity'ye bağımlı değildir, EditMode testle doğrulanır.
    /// </summary>
    public sealed class ParentGateSession
    {
        private readonly Random random;
        private int wrongAttempts;
        private double lockedUntil;

        public int Left { get; private set; }
        public int Right { get; private set; }

        public string Question => Left + " + " + Right + " = ?";

        public ParentGateSession(Random random)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            NextQuestion();
        }

        public bool IsLockedOut(double nowSeconds) => nowSeconds < lockedUntil;

        public double RemainingLockSeconds(double nowSeconds) =>
            IsLockedOut(nowSeconds) ? lockedUntil - nowSeconds : 0d;

        public GateResult Submit(int answer, double nowSeconds)
        {
            if (IsLockedOut(nowSeconds)) return GateResult.LockedOut;

            if (answer == Left + Right)
            {
                wrongAttempts = 0;
                return GateResult.Passed;
            }

            wrongAttempts++;
            NextQuestion();
            if (wrongAttempts >= ParentGateRules.MaxWrongAttempts)
            {
                wrongAttempts = 0;
                lockedUntil = nowSeconds + ParentGateRules.LockoutSeconds;
                return GateResult.LockedOut;
            }
            return GateResult.Wrong;
        }

        private void NextQuestion()
        {
            Left = random.Next(ParentGateRules.MinLeft, ParentGateRules.MaxLeft + 1);
            Right = random.Next(ParentGateRules.MinRight, ParentGateRules.MaxRight + 1);
        }
    }
}
