using System;
using System.Collections.Generic;

namespace MinikDuello.Domain.Rounds
{
    public enum CardState
    {
        Hidden,
        Selected,
        Matched
    }

    public enum PairTap
    {
        Ignored,
        Selected,
        Match,
        Mismatch,
        Completed
    }

    /// <summary>
    /// Eşleştirme/hafıza mantığı (UI'dan bağımsız). İki kart açık ve eşleşmezse "bekleyen uyuşmazlık" oluşur;
    /// UI kısa süre gösterip ClearMismatch çağırır. Uyuşmazlık beklerken dokunmalar yok sayılır.
    /// </summary>
    public sealed class PairsLogic
    {
        private readonly List<PairCard> cards;
        private readonly CardState[] states;
        private int first = -1;
        private int pendingA = -1;
        private int pendingB = -1;

        public int Mistakes { get; private set; }

        public PairsLogic(PairsRound round)
        {
            cards = round.Cards;
            states = new CardState[cards.Count];
        }

        public int Count => cards.Count;
        public CardState StateOf(int index) => states[index];
        public bool HasPendingMismatch => pendingA >= 0;
        public int PendingA => pendingA;
        public int PendingB => pendingB;

        public bool IsDone
        {
            get
            {
                foreach (CardState s in states)
                {
                    if (s != CardState.Matched) return false;
                }
                return true;
            }
        }

        public PairTap Tap(int index)
        {
            if (index < 0 || index >= cards.Count) return PairTap.Ignored;
            if (HasPendingMismatch || states[index] != CardState.Hidden) return PairTap.Ignored;

            if (first < 0)
            {
                first = index;
                states[index] = CardState.Selected;
                return PairTap.Selected;
            }

            if (cards[first].PairKey == cards[index].PairKey)
            {
                states[first] = CardState.Matched;
                states[index] = CardState.Matched;
                first = -1;
                return IsDone ? PairTap.Completed : PairTap.Match;
            }

            states[index] = CardState.Selected;
            pendingA = first;
            pendingB = index;
            first = -1;
            Mistakes++;
            return PairTap.Mismatch;
        }

        public void ClearMismatch()
        {
            if (!HasPendingMismatch) return;
            states[pendingA] = CardState.Hidden;
            states[pendingB] = CardState.Hidden;
            pendingA = -1;
            pendingB = -1;
        }

        /// <summary>İpucu: henüz eşleşmemiş bir çiftin iki kartı (yoksa null).</summary>
        public int[] HintPair()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (states[i] == CardState.Matched) continue;
                for (int j = i + 1; j < cards.Count; j++)
                {
                    if (states[j] != CardState.Matched && cards[i].PairKey == cards[j].PairKey) return new[] { i, j };
                }
            }
            return null;
        }
    }
}
