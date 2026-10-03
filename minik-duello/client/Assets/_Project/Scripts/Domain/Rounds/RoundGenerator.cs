using System;
using System.Collections.Generic;
using MinikDuello.Domain.Levels;

namespace MinikDuello.Domain.Rounds
{
    /// <summary>
    /// Bölüm yapılandırmasından tur listesi üretir. Tüm oyun türleri dört etkileşim şablonuna (Choice, Pairs, Sort, Maze) indirgenir;
    /// böylece 100 bölüm için bölüm başına ayrı kod gerekmez.
    /// </summary>
    public sealed class RoundGenerator
    {
        private const int MinOptions = 2;
        private const int MaxOptions = 6;
        private const int MinMemoryCards = 4;
        private const int ExtraSortItems = 2;
        private const int QuickFindExtra = 3;
        private const int QuickFindMin = 4;
        private const int QuickFindMax = 9;
        private const double PatternAbOnlyBelow = 0.52;
        private const double PatternTwoSymbolBelow = 0.56;
        private const int MixMemoryCards = 4;

        private static readonly string[] MixTypes =
        {
            GameTypes.FindColor, GameTypes.FindShape, GameTypes.CountSelect, GameTypes.MemoryCards,
            GameTypes.Pattern, GameTypes.FindAnimal, GameTypes.QuickFind, GameTypes.AnimalSound
        };

        private readonly Random random;
        private int idCounter;

        public RoundGenerator(Random random)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public List<RoundSpec> Generate(LevelConfig level)
        {
            var rounds = new List<RoundSpec>();
            for (int i = 0; i < level.RoundCount; i++)
            {
                string type = level.GameType == GameTypes.ExplorerMix
                    ? MixTypes[random.Next(MixTypes.Length)]
                    : level.GameType;
                rounds.Add(GenerateOne(type, level));
            }
            return rounds;
        }

        public RoundSpec GenerateOne(string gameType, LevelConfig level)
        {
            switch (gameType)
            {
                case GameTypes.ColorMatch: return PairsOf(Content.Colors, VisualKind.Color, level.ObjectCount, level.ColorCount, false, "matchColors");
                case GameTypes.ShapeMatch: return PairsOf(Content.Shapes, VisualKind.Shape, level.ObjectCount, Clamp(level.ObjectCount, MinOptions, MaxOptions), false, "matchShapes");
                case GameTypes.AnimalPair: return PairsOf(Content.Animals, VisualKind.Animal, level.ObjectCount, Clamp(level.ObjectCount, MinOptions, MaxOptions), false, "matchAnimals");
                case GameTypes.MemoryCards: return MemoryRound(level.ObjectCount);
                case GameTypes.ColorBox: return SortBy(Content.Colors, VisualKind.Color, level.ColorCount, level.ObjectCount + level.ColorCount, "sortColors");
                case GameTypes.ShapeSort: return SortBy(Content.Shapes, VisualKind.Shape, Clamp(level.ObjectCount - 1, MinOptions, 4), level.ObjectCount + ExtraSortItems, "sortShapes");
                case GameTypes.AnimalHabitat: return HabitatRound(level.ObjectCount);
                case GameTypes.Puzzle: return PuzzleRound(level.ObjectCount);
                case GameTypes.FindColor: return FindRound(Content.Colors, VisualKind.Color, Clamp(level.ColorCount, MinOptions, MaxOptions), "findColor");
                case GameTypes.FindShape: return FindRound(Content.Shapes, VisualKind.Shape, Clamp(level.ObjectCount + 1, MinOptions, MaxOptions), "findShape");
                case GameTypes.FindAnimal: return FindRound(Content.Animals, VisualKind.Animal, Clamp(level.ObjectCount, MinOptions, MaxOptions), "findAnimal");
                case GameTypes.MissingColor: return MissingColorRound(level.ColorCount);
                case GameTypes.CountSelect: return CountRound(level.ObjectCount);
                case GameTypes.AnimalSound: return SoundRound(Clamp(level.ObjectCount, MinOptions, 4));
                case GameTypes.QuickFind: return QuickFindRound(level.ObjectCount);
                case GameTypes.Pattern: return PatternRound(level.Difficulty);
                case GameTypes.Maze: return MazeGenerator.Generate(level.ObjectCount, level.ObjectCount, random);
                default: throw new ArgumentException("Bilinmeyen oyun türü: " + gameType, nameof(gameType));
            }
        }

        // --- Eşleştirme ---

        private PairsRound PairsOf(string[] source, VisualKind kind, int pairCount, int distinct, bool faceDown, string prompt)
        {
            List<string> pool = Sample(source, Clamp(distinct, 1, source.Length));
            var round = new PairsRound { PromptKey = prompt, FaceDown = faceDown };
            for (int i = 0; i < pairCount; i++)
            {
                string key = pool[i % pool.Count];
                // Aynı anahtarlı kartlar birbiriyle eşleşir (aynı renkten iki çift olabilir).
                round.Cards.Add(new PairCard { Id = NextId("c"), PairKey = key, Visual = new VisualRef(kind, key) });
                round.Cards.Add(new PairCard { Id = NextId("c"), PairKey = key, Visual = new VisualRef(kind, key) });
            }
            Shuffle(round.Cards);
            return round;
        }

        private PairsRound MemoryRound(int cardCount)
        {
            int even = Math.Max(MinMemoryCards, cardCount - cardCount % 2);
            int pairs = even / 2;
            // Sembol + şekil havuzu: 12 karta (6 çift) kadar benzersiz çift sağlar.
            var pool = new List<VisualRef>();
            foreach (string s in Content.Symbols) pool.Add(new VisualRef(VisualKind.Symbol, s));
            foreach (string s in Content.Shapes) pool.Add(new VisualRef(VisualKind.Shape, s));
            Shuffle(pool);

            var round = new PairsRound { PromptKey = "memoryPairs", FaceDown = true };
            for (int i = 0; i < pairs; i++)
            {
                VisualRef v = pool[i % pool.Count];
                string key = v.Kind + ":" + v.Key + ":" + i;
                round.Cards.Add(new PairCard { Id = NextId("m"), PairKey = key, Visual = v });
                round.Cards.Add(new PairCard { Id = NextId("m"), PairKey = key, Visual = v });
            }
            Shuffle(round.Cards);
            return round;
        }

        // --- Sürükle-bırak ---

        private SortRound SortBy(string[] source, VisualKind kind, int binCount, int itemCount, string prompt)
        {
            List<string> bins = Sample(source, Clamp(binCount, MinOptions, source.Length));
            var round = new SortRound { PromptKey = prompt };
            foreach (string b in bins) round.Bins.Add(new SortBin { Id = b, Visual = new VisualRef(kind, b) });

            int total = Math.Max(itemCount, bins.Count);
            for (int i = 0; i < total; i++)
            {
                // İlk turda her kutuya en az bir nesne düşer; kalanlar rastgele.
                string key = i < bins.Count ? bins[i] : bins[random.Next(bins.Count)];
                round.Items.Add(new SortItem { Id = NextId("s"), Visual = new VisualRef(kind, key), TargetBinId = key });
            }
            Shuffle(round.Items);
            return round;
        }

        private SortRound HabitatRound(int objectCount)
        {
            int binCount = Clamp(objectCount - 1, MinOptions, Content.Habitats.Length);
            List<string> habitats = Sample(Content.Habitats, binCount);
            var round = new SortRound { PromptKey = "sortHabitats" };
            foreach (string h in habitats) round.Bins.Add(new SortBin { Id = h, Visual = new VisualRef(VisualKind.Symbol, h) });

            // Her yaşam alanına en az bir hayvan düşer; kalanlar rastgele tamamlanır.
            var chosen = new List<string>();
            foreach (string h in habitats)
            {
                var forHabitat = new List<string>();
                foreach (string a in Content.HabitatAnimals)
                {
                    if (Content.AnimalHabitat[a] == h) forHabitat.Add(a);
                }
                chosen.Add(forHabitat[random.Next(forHabitat.Count)]);
            }
            var rest = new List<string>();
            foreach (string a in Content.HabitatAnimals)
            {
                if (habitats.Contains(Content.AnimalHabitat[a]) && !chosen.Contains(a)) rest.Add(a);
            }
            Shuffle(rest);
            int target = Math.Min(chosen.Count + rest.Count, Math.Max(habitats.Count, objectCount + 1));
            for (int i = 0; chosen.Count < target; i++) chosen.Add(rest[i]);
            Shuffle(chosen);
            foreach (string animal in chosen)
            {
                round.Items.Add(new SortItem { Id = NextId("a"), Visual = new VisualRef(VisualKind.Animal, animal), TargetBinId = Content.AnimalHabitat[animal] });
            }
            return round;
        }

        private SortRound PuzzleRound(int pieces)
        {
            string image = Content.PuzzleImages[random.Next(Content.PuzzleImages.Length)];
            var round = new SortRound { PromptKey = "puzzleAssemble", SingleSlot = true };
            round.PromptParams["image"] = image;
            for (int i = 0; i < pieces; i++)
            {
                string slot = "slot" + i;
                round.Bins.Add(new SortBin { Id = slot, Visual = new VisualRef(VisualKind.Piece, image + "_" + i + "_slot") });
                round.Items.Add(new SortItem { Id = NextId("p"), Visual = new VisualRef(VisualKind.Piece, image + "_" + i), TargetBinId = slot });
            }
            Shuffle(round.Items);
            return round;
        }

        // --- Seçmeli ---

        private ChoiceRound FindRound(string[] source, VisualKind kind, int optionCount, string prompt)
        {
            List<string> keys = Sample(source, Clamp(optionCount, MinOptions, source.Length));
            string target = keys[random.Next(keys.Count)];
            var round = new ChoiceRound { PromptKey = prompt, CorrectId = target };
            round.PromptParams["target"] = target;
            foreach (string k in keys) round.Options.Add(new ChoiceOption { Id = k, Visual = new VisualRef(kind, k) });
            return round;
        }

        private ChoiceRound MissingColorRound(int colorCount)
        {
            List<string> pool = Sample(Content.Colors, Clamp(colorCount, MinOptions, Content.Colors.Length));
            string missing = pool[pool.Count - 1];
            var round = new ChoiceRound { PromptKey = "missingColor", CorrectId = missing };
            for (int i = 0; i < pool.Count - 1; i++) round.Sequence.Add(new VisualRef(VisualKind.Color, pool[i]));
            round.Sequence.Add(new VisualRef(VisualKind.Unknown, "?"));
            Shuffle(pool);
            foreach (string c in pool) round.Options.Add(new ChoiceOption { Id = c, Visual = new VisualRef(VisualKind.Color, c) });
            return round;
        }

        private ChoiceRound CountRound(int objectCount)
        {
            int max = Clamp(objectCount, 1, Content.NumberMax);
            int n = random.Next(1, max + 1);
            var round = new ChoiceRound
            {
                PromptKey = "countSelect",
                CorrectId = n.ToString(),
                CountToShow = n,
                CountVisual = new VisualRef(VisualKind.Symbol, "balloon")
            };
            round.PromptParams["count"] = n.ToString();

            int poolMax = Math.Min(Content.NumberMax, Math.Max(max, 4));
            var numbers = new List<int>();
            for (int i = Content.NumberMin; i <= poolMax; i++) numbers.Add(i);
            numbers.Remove(n);
            Shuffle(numbers);
            var options = new List<int> { n };
            for (int i = 0; i < 2 && i < numbers.Count; i++) options.Add(numbers[i]);
            Shuffle(options);
            foreach (int o in options) round.Options.Add(new ChoiceOption { Id = o.ToString(), Visual = new VisualRef(VisualKind.Number, o.ToString()) });
            return round;
        }

        private ChoiceRound SoundRound(int optionCount)
        {
            List<string> animals = Sample(Content.SoundAnimals, Clamp(optionCount, MinOptions, Content.SoundAnimals.Length));
            string target = animals[random.Next(animals.Count)];
            var round = new ChoiceRound { PromptKey = "animalSound", CorrectId = target, SoundText = Content.AnimalSound[target] };
            round.PromptParams["target"] = target;
            foreach (string a in animals) round.Options.Add(new ChoiceOption { Id = a, Visual = new VisualRef(VisualKind.Animal, a) });
            return round;
        }

        private ChoiceRound QuickFindRound(int objectCount)
        {
            int total = Clamp(objectCount + QuickFindExtra, QuickFindMin, QuickFindMax);
            string target = Content.Shapes[random.Next(Content.Shapes.Length)];
            var others = new List<string>();
            foreach (string s in Content.Shapes)
            {
                if (s != target) others.Add(s);
            }

            var round = new ChoiceRound { PromptKey = "findShape", Grid = true };
            round.PromptParams["target"] = target;
            int targetIndex = random.Next(total);
            for (int i = 0; i < total; i++)
            {
                string key = i == targetIndex ? target : others[random.Next(others.Count)];
                string id = "q" + i;
                if (i == targetIndex) round.CorrectId = id;
                round.Options.Add(new ChoiceOption { Id = id, Visual = new VisualRef(VisualKind.Shape, key) });
            }
            return round;
        }

        private ChoiceRound PatternRound(double difficulty)
        {
            string[][] templates;
            if (difficulty < PatternAbOnlyBelow) templates = new[] { new[] { "A", "B" } };
            else if (difficulty < PatternTwoSymbolBelow) templates = new[] { new[] { "A", "B" }, new[] { "A", "A", "B" }, new[] { "A", "B", "B" } };
            else templates = new[] { new[] { "A", "B" }, new[] { "A", "A", "B" }, new[] { "A", "B", "B" }, new[] { "A", "B", "C" } };

            string[] template = templates[random.Next(templates.Length)];
            bool useColors = random.Next(2) == 0;
            string[] source = useColors ? Content.Colors : Content.Shapes;
            VisualKind kind = useColors ? VisualKind.Color : VisualKind.Shape;

            var symbols = new List<string>();
            foreach (string t in template)
            {
                if (!symbols.Contains(t)) symbols.Add(t);
            }
            List<string> picks = Sample(source, symbols.Count + 1);
            var map = new Dictionary<string, string>();
            for (int i = 0; i < symbols.Count; i++) map[symbols[i]] = picks[i];

            int shown = template.Length == 2 ? 4 : 5;
            var round = new ChoiceRound { PromptKey = "pattern" };
            for (int i = 0; i < shown; i++) round.Sequence.Add(new VisualRef(kind, map[template[i % template.Length]]));
            round.Sequence.Add(new VisualRef(VisualKind.Unknown, "?"));
            round.CorrectId = map[template[shown % template.Length]];

            var optionKeys = new List<string>(map.Values);
            optionKeys.Add(picks[picks.Count - 1]); // fazladan çeldirici
            Shuffle(optionKeys);
            foreach (string k in optionKeys) round.Options.Add(new ChoiceOption { Id = k, Visual = new VisualRef(kind, k) });
            return round;
        }

        // --- Yardımcılar ---

        private string NextId(string prefix)
        {
            idCounter++;
            return prefix + idCounter;
        }

        private static int Clamp(int value, int min, int max) => value < min ? min : (value > max ? max : value);

        private List<T> Sample<T>(IList<T> source, int count)
        {
            var copy = new List<T>(source);
            Shuffle(copy);
            return copy.GetRange(0, Math.Min(count, copy.Count));
        }

        private void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}
