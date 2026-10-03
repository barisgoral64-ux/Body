using System;
using System.IO;
using System.Linq;
using MinikDuello.Domain.Levels;
using MinikDuello.Domain.Rounds;
using NUnit.Framework;

namespace MinikDuello.Tests
{
    public sealed class RoundGeneratorTests
    {
        private const int SeedsPerLevel = 20;

        private static LevelCatalog Load() => LevelCatalog.FromJson(File.ReadAllText(TestPaths.LevelsJson()), 100, 10);

        [Test]
        public void AllLevels_ProduceValidRounds_ForManySeeds()
        {
            LevelCatalog catalog = Load();
            foreach (LevelConfig level in catalog.All)
            {
                for (int seed = 0; seed < SeedsPerLevel; seed++)
                {
                    var gen = new RoundGenerator(new Random(seed * 1000 + level.LevelId));
                    var rounds = gen.Generate(level);
                    Assert.AreEqual(level.RoundCount, rounds.Count, "Bölüm " + level.LevelId);
                    foreach (RoundSpec round in rounds) AssertValid(round, level, seed);
                }
            }
        }

        [Test]
        public void ExplorerMix_UsesVariedGameTypes()
        {
            LevelConfig level = Load().Get(95);
            var archetypes = new System.Collections.Generic.HashSet<string>();
            for (int seed = 0; seed < 40; seed++)
            {
                foreach (RoundSpec r in new RoundGenerator(new Random(seed)).Generate(level)) archetypes.Add(r.PromptKey);
            }
            Assert.GreaterOrEqual(archetypes.Count, 4);
        }

        [Test]
        public void WorldFive_MemoryStartsWithFourCards_AndGrows()
        {
            LevelCatalog catalog = Load();
            var first = (PairsRound)new RoundGenerator(new Random(1)).Generate(catalog.Get(41))[0];
            var last = (PairsRound)new RoundGenerator(new Random(1)).Generate(catalog.Get(50))[0];
            Assert.AreEqual(4, first.Cards.Count);
            Assert.IsTrue(first.FaceDown);
            Assert.GreaterOrEqual(last.Cards.Count, 10);
        }

        [Test]
        public void Puzzle_PiecesRangeFromTwoToSix()
        {
            LevelCatalog catalog = Load();
            Assert.AreEqual(2, ((SortRound)new RoundGenerator(new Random(1)).Generate(catalog.Get(81))[0]).Items.Count);
            Assert.AreEqual(6, ((SortRound)new RoundGenerator(new Random(1)).Generate(catalog.Get(90))[0]).Items.Count);
        }

        [Test]
        public void Score_NeverBelowMinimum_NeverNegative()
        {
            Assert.AreEqual(ScoreCalculator.FullPoints, ScoreCalculator.RoundPoints(0));
            for (int mistakes = 0; mistakes < 50; mistakes++)
            {
                Assert.GreaterOrEqual(ScoreCalculator.RoundPoints(mistakes), ScoreCalculator.MinPoints);
            }
            Assert.AreEqual(ScoreCalculator.MinPoints, ScoreCalculator.RoundPoints(-3) == ScoreCalculator.FullPoints ? ScoreCalculator.MinPoints : 0);
        }

        [Test]
        public void CompletingLevelWithMistakes_StillEarnsAtLeastOneStar()
        {
            foreach (LevelConfig level in Load().All)
            {
                int worst = ScoreCalculator.MinPoints * level.RoundCount;
                Assert.GreaterOrEqual(StarCalculator.StarsForScore(level, worst), 1, "Bölüm " + level.LevelId);
                Assert.AreEqual(3, StarCalculator.StarsForScore(level, ScoreCalculator.FullPoints * level.RoundCount));
            }
        }

        [Test]
        public void Maze_IsFullyConnected_AndGoalIsFarthest()
        {
            for (int size = 2; size <= 9; size++)
            {
                for (int seed = 0; seed < 25; seed++)
                {
                    MazeRound maze = MazeGenerator.Generate(size, size, new Random(seed));
                    int[] dist = MazeGenerator.Distances(maze.Walls, size, size, maze.StartX, maze.StartY);
                    Assert.IsTrue(dist.All(d => d >= 0), "Ulaşılamayan hücre var");
                    Assert.AreEqual(dist.Max(), dist[maze.GoalY * size + maze.GoalX]);
                    Assert.Greater(dist[maze.GoalY * size + maze.GoalX], 0);
                    // Mükemmel labirent: duvarları simetrik, açık geçit sayısı = hücre - 1.
                    int openings = 0;
                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            if (x < size - 1 && MazeGenerator.CanMove(maze, x, y, Wall.East))
                            {
                                openings++;
                                Assert.IsTrue(MazeGenerator.CanMove(maze, x + 1, y, Wall.West));
                            }
                            if (y < size - 1 && MazeGenerator.CanMove(maze, x, y, Wall.South))
                            {
                                openings++;
                                Assert.IsTrue(MazeGenerator.CanMove(maze, x, y + 1, Wall.North));
                            }
                        }
                    }
                    Assert.AreEqual(size * size - 1, openings);
                }
            }
        }

        [Test]
        public void Prompts_AreInTurkish_AndNeverNegative()
        {
            var p = new System.Collections.Generic.Dictionary<string, string> { { "target", "red" } };
            Assert.AreEqual("Kırmızı rengi bul!", Prompts.Text("findColor", p));
            Assert.AreEqual(string.Empty, Prompts.Text("bilinmeyen", p));
        }

        // --- Doğrulama ---

        private static void AssertValid(RoundSpec round, LevelConfig level, int seed)
        {
            string where = " (bölüm " + level.LevelId + ", tohum " + seed + ", " + level.GameType + ")";
            Assert.IsFalse(string.IsNullOrEmpty(round.PromptKey), "PromptKey boş" + where);
            switch (round)
            {
                case ChoiceRound c:
                    Assert.GreaterOrEqual(c.Options.Count, 2, "Seçenek az" + where);
                    CollectionAssert.AllItemsAreUnique(c.Options.Select(o => o.Id), "Seçenek kimlikleri benzersiz değil" + where);
                    Assert.AreEqual(1, c.Options.Count(o => o.Id == c.CorrectId), "Doğru cevap tam bir kez olmalı" + where);
                    if (c.Grid)
                    {
                        string target = c.PromptParams["target"];
                        Assert.AreEqual(1, c.Options.Count(o => o.Visual.Key == target), "Hedef tam bir kez görünmeli" + where);
                    }
                    if (c.CountToShow > 0) Assert.AreEqual(c.CorrectId, c.CountToShow.ToString(), where);
                    if (c.Sequence.Count > 0) Assert.AreEqual(VisualKind.Unknown, c.Sequence.Last().Kind, where);
                    break;
                case PairsRound p:
                    Assert.AreEqual(0, p.Cards.Count % 2, "Kart sayısı çift olmalı" + where);
                    CollectionAssert.AllItemsAreUnique(p.Cards.Select(x => x.Id), where);
                    foreach (var g in p.Cards.GroupBy(x => x.PairKey)) Assert.AreEqual(0, g.Count() % 2, "Eşsiz çift" + where);
                    break;
                case SortRound s:
                    Assert.GreaterOrEqual(s.Bins.Count, 2 - (s.SingleSlot ? 1 : 0), where);
                    var binIds = s.Bins.Select(b => b.Id).ToList();
                    CollectionAssert.AllItemsAreUnique(binIds, where);
                    CollectionAssert.AllItemsAreUnique(s.Items.Select(i => i.Id), where);
                    foreach (SortItem item in s.Items) CollectionAssert.Contains(binIds, item.TargetBinId, "Hedef kutu yok" + where);
                    if (s.SingleSlot) Assert.AreEqual(s.Items.Count, s.Bins.Count, where);
                    else foreach (string bin in binIds) Assert.IsTrue(s.Items.Any(i => i.TargetBinId == bin), "Boş kutu" + where);
                    break;
                case MazeRound m:
                    int[] dist = MazeGenerator.Distances(m.Walls, m.Width, m.Height, m.StartX, m.StartY);
                    Assert.GreaterOrEqual(dist[m.GoalY * m.Width + m.GoalX], 1, "Hedef ulaşılamaz" + where);
                    break;
                default:
                    Assert.Fail("Bilinmeyen tur türü" + where);
                    break;
            }
        }
    }
}
