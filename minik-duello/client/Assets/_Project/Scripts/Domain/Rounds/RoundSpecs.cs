using System.Collections.Generic;

namespace MinikDuello.Domain.Rounds
{
    /// <summary>Tur etkileşim türleri: UI yalnızca bu dört şablonu çizer.</summary>
    public enum RoundArchetype
    {
        Choice,
        Pairs,
        Sort,
        Maze
    }

    public abstract class RoundSpec
    {
        public abstract RoundArchetype Archetype { get; }

        /// <summary>Sesli yönerge ve metin anahtarı (örn. findColor).</summary>
        public string PromptKey;

        /// <summary>Yönergedeki değişkenler (örn. target=red).</summary>
        public Dictionary<string, string> PromptParams = new Dictionary<string, string>();
    }

    public sealed class ChoiceOption
    {
        public string Id;
        public VisualRef Visual;
    }

    /// <summary>Tek doğru seçenek: renk bul, şekil bul, say, desen tamamla, eksik rengi bul...</summary>
    public sealed class ChoiceRound : RoundSpec
    {
        public override RoundArchetype Archetype => RoundArchetype.Choice;
        public List<ChoiceOption> Options = new List<ChoiceOption>();
        public string CorrectId;

        /// <summary>Desen/eksik renk için gösterilen dizi; boş (Unknown) eleman "?" kutusudur.</summary>
        public List<VisualRef> Sequence = new List<VisualRef>();

        /// <summary>Saydırma görevlerinde ekranda gösterilecek nesne sayısı (0 = yok).</summary>
        public int CountToShow;
        public VisualRef CountVisual;

        /// <summary>Hızlı seçim/bulma ızgarası mı (çok seçenek, küçük düğme)?</summary>
        public bool Grid;

        /// <summary>Ses tanıma için gösterilen metin balonu (hayvan sesi).</summary>
        public string SoundText;
    }

    public sealed class PairCard
    {
        public string Id;
        public string PairKey;
        public VisualRef Visual;
    }

    /// <summary>Eşleştirme / hafıza kartları. FaceDown=true ise kartlar kapalı başlar.</summary>
    public sealed class PairsRound : RoundSpec
    {
        public override RoundArchetype Archetype => RoundArchetype.Pairs;
        public List<PairCard> Cards = new List<PairCard>();
        public bool FaceDown;
        public int PairCount => Cards.Count / 2;
    }

    public sealed class SortItem
    {
        public string Id;
        public VisualRef Visual;
        public string TargetBinId;
    }

    public sealed class SortBin
    {
        public string Id;
        public VisualRef Visual;
    }

    /// <summary>Sürükle-bırak: nesneleri doğru kutuya/yuvaya taşı. SingleSlot=true: puzzle (her yuva tek parça).</summary>
    public sealed class SortRound : RoundSpec
    {
        public override RoundArchetype Archetype => RoundArchetype.Sort;
        public List<SortItem> Items = new List<SortItem>();
        public List<SortBin> Bins = new List<SortBin>();
        public bool SingleSlot;
    }

    [System.Flags]
    public enum Wall
    {
        None = 0,
        North = 1,
        East = 2,
        South = 4,
        West = 8
    }

    public sealed class MazeRound : RoundSpec
    {
        public override RoundArchetype Archetype => RoundArchetype.Maze;
        public int Width;
        public int Height;
        /// <summary>Hücre başına açık duvarlar (Wall bayrakları). İndeks = y * Width + x.</summary>
        public Wall[] Walls;
        public int StartX;
        public int StartY;
        public int GoalX;
        public int GoalY;
    }
}
