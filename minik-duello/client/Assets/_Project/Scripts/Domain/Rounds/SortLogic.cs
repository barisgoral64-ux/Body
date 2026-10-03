using System.Collections.Generic;

namespace MinikDuello.Domain.Rounds
{
    public enum PlaceResult
    {
        Ignored,
        Correct,
        Wrong,
        Completed
    }

    /// <summary>
    /// Sürükle-bırak sıralama/puzzle mantığı. Yanlış kutu yalnızca hata sayar (nesne geri döner);
    /// tek yuvalı modda (puzzle) dolu yuvaya ikinci parça yerleştirilemez.
    /// </summary>
    public sealed class SortLogic
    {
        private readonly SortRound round;
        private readonly HashSet<string> placed = new HashSet<string>();
        private readonly Dictionary<string, int> binFill = new Dictionary<string, int>();

        public int Mistakes { get; private set; }

        public SortLogic(SortRound round)
        {
            this.round = round;
            foreach (SortBin bin in round.Bins) binFill[bin.Id] = 0;
        }

        public int Remaining => round.Items.Count - placed.Count;
        public bool IsDone => Remaining == 0;
        public bool IsPlaced(string itemId) => placed.Contains(itemId);
        public int FillOf(string binId) => binFill.TryGetValue(binId, out int n) ? n : 0;

        public PlaceResult TryPlace(string itemId, string binId)
        {
            SortItem item = Find(itemId);
            if (item == null || placed.Contains(itemId) || !binFill.ContainsKey(binId)) return PlaceResult.Ignored;
            if (round.SingleSlot && binFill[binId] > 0) return PlaceResult.Ignored;

            if (item.TargetBinId != binId)
            {
                Mistakes++;
                return PlaceResult.Wrong;
            }

            placed.Add(itemId);
            binFill[binId]++;
            return IsDone ? PlaceResult.Completed : PlaceResult.Correct;
        }

        /// <summary>İpucu: yerleştirilmemiş ilk nesne ve doğru kutusu.</summary>
        public SortItem HintItem()
        {
            foreach (SortItem item in round.Items)
            {
                if (!placed.Contains(item.Id)) return item;
            }
            return null;
        }

        private SortItem Find(string id)
        {
            foreach (SortItem item in round.Items)
            {
                if (item.Id == id) return item;
            }
            return null;
        }
    }
}
