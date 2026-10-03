using System;
using System.IO;
using System.Linq;
using MinikDuello.Domain.Levels;
using MinikDuello.Domain.Rounds;
using NUnit.Framework;

namespace MinikDuello.Tests
{
    public sealed class GameLogicTests
    {
        private static PairsRound Pairs(params string[] keys)
        {
            var round = new PairsRound();
            int n = 0;
            foreach (string k in keys) round.Cards.Add(new PairCard { Id = "c" + n++, PairKey = k, Visual = new VisualRef(VisualKind.Color, k) });
            return round;
        }

        // --- PairsLogic ---

        [Test]
        public void Pairs_MatchFlow_CompletesWithoutMistakes()
        {
            var logic = new PairsLogic(Pairs("a", "b", "a", "b"));
            Assert.AreEqual(PairTap.Selected, logic.Tap(0));
            Assert.AreEqual(PairTap.Match, logic.Tap(2));
            Assert.AreEqual(PairTap.Selected, logic.Tap(1));
            Assert.AreEqual(PairTap.Completed, logic.Tap(3));
            Assert.IsTrue(logic.IsDone);
            Assert.AreEqual(0, logic.Mistakes);
        }

        [Test]
        public void Pairs_Mismatch_CountsMistake_BlocksInputUntilCleared()
        {
            var logic = new PairsLogic(Pairs("a", "b", "a", "b"));
            logic.Tap(0);
            Assert.AreEqual(PairTap.Mismatch, logic.Tap(1));
            Assert.AreEqual(1, logic.Mistakes);
            Assert.IsTrue(logic.HasPendingMismatch);
            Assert.AreEqual(PairTap.Ignored, logic.Tap(2)); // uyuşmazlık beklerken dokunma yok sayılır

            logic.ClearMismatch();
            Assert.AreEqual(CardState.Hidden, logic.StateOf(0));
            Assert.AreEqual(CardState.Hidden, logic.StateOf(1));
            Assert.AreEqual(PairTap.Selected, logic.Tap(0));
        }

        [Test]
        public void Pairs_IgnoresInvalidAndRepeatedTaps()
        {
            var logic = new PairsLogic(Pairs("a", "a"));
            Assert.AreEqual(PairTap.Ignored, logic.Tap(-1));
            Assert.AreEqual(PairTap.Ignored, logic.Tap(5));
            logic.Tap(0);
            Assert.AreEqual(PairTap.Ignored, logic.Tap(0)); // aynı karta ikinci dokunuş
            Assert.AreEqual(PairTap.Completed, logic.Tap(1));
            Assert.AreEqual(PairTap.Ignored, logic.Tap(1));
        }

        [Test]
        public void Pairs_HintPointsAtRealPair()
        {
            var logic = new PairsLogic(Pairs("a", "b", "a", "b"));
            int[] hint = logic.HintPair();
            CollectionAssert.AreEqual(new[] { 0, 2 }, hint);
            logic.Tap(0);
            logic.Tap(2);
            CollectionAssert.AreEqual(new[] { 1, 3 }, logic.HintPair());
            logic.Tap(1);
            logic.Tap(3);
            Assert.IsNull(logic.HintPair());
        }

        [Test]
        public void Pairs_PlayThroughGeneratedRounds_AlwaysTerminates()
        {
            var catalog = LevelCatalog.FromJson(File.ReadAllText(TestPaths.LevelsJson()), 100, 10);
            foreach (string type in new[] { GameTypes.ColorMatch, GameTypes.ShapeMatch, GameTypes.AnimalPair, GameTypes.MemoryCards })
            {
                LevelConfig level = catalog.All.First(l => l.GameType == type);
                var round = (PairsRound)new RoundGenerator(new Random(7)).GenerateOne(type, level);
                var logic = new PairsLogic(round);
                for (int i = 0; i < round.Cards.Count && !logic.IsDone; i++)
                {
                    if (logic.StateOf(i) != CardState.Hidden) continue;
                    logic.Tap(i);
                    int partner = Enumerable.Range(0, round.Cards.Count).First(j => j != i && logic.StateOf(j) == CardState.Hidden && round.Cards[j].PairKey == round.Cards[i].PairKey);
                    logic.Tap(partner);
                }
                Assert.IsTrue(logic.IsDone, type);
                Assert.AreEqual(0, logic.Mistakes, type);
            }
        }

        // --- SortLogic ---

        private static SortRound Sort(bool single)
        {
            var round = new SortRound { SingleSlot = single };
            round.Bins.Add(new SortBin { Id = "red" });
            round.Bins.Add(new SortBin { Id = "blue" });
            round.Items.Add(new SortItem { Id = "i1", TargetBinId = "red" });
            round.Items.Add(new SortItem { Id = "i2", TargetBinId = "blue" });
            round.Items.Add(new SortItem { Id = "i3", TargetBinId = "red" });
            return round;
        }

        [Test]
        public void Sort_CorrectPlacements_Complete()
        {
            var logic = new SortLogic(Sort(false));
            Assert.AreEqual(PlaceResult.Correct, logic.TryPlace("i1", "red"));
            Assert.AreEqual(PlaceResult.Correct, logic.TryPlace("i2", "blue"));
            Assert.AreEqual(PlaceResult.Completed, logic.TryPlace("i3", "red"));
            Assert.IsTrue(logic.IsDone);
            Assert.AreEqual(2, logic.FillOf("red"));
        }

        [Test]
        public void Sort_WrongBin_CountsMistake_ButItemStaysAvailable()
        {
            var logic = new SortLogic(Sort(false));
            Assert.AreEqual(PlaceResult.Wrong, logic.TryPlace("i1", "blue"));
            Assert.AreEqual(1, logic.Mistakes);
            Assert.IsFalse(logic.IsPlaced("i1"));
            Assert.AreEqual(PlaceResult.Correct, logic.TryPlace("i1", "red"));
        }

        [Test]
        public void Sort_IgnoresUnknownAndAlreadyPlaced()
        {
            var logic = new SortLogic(Sort(false));
            logic.TryPlace("i1", "red");
            Assert.AreEqual(PlaceResult.Ignored, logic.TryPlace("i1", "red"));
            Assert.AreEqual(PlaceResult.Ignored, logic.TryPlace("yok", "red"));
            Assert.AreEqual(PlaceResult.Ignored, logic.TryPlace("i2", "yok"));
            Assert.AreEqual(0, logic.Mistakes);
        }

        [Test]
        public void Sort_SingleSlot_RejectsSecondItemInSlot()
        {
            var round = new SortRound { SingleSlot = true };
            round.Bins.Add(new SortBin { Id = "s0" });
            round.Items.Add(new SortItem { Id = "p0", TargetBinId = "s0" });
            round.Items.Add(new SortItem { Id = "p1", TargetBinId = "s0" }); // aynı hedef: yalnızca test için
            var logic = new SortLogic(round);
            Assert.AreEqual(PlaceResult.Correct, logic.TryPlace("p0", "s0"));
            Assert.AreEqual(PlaceResult.Ignored, logic.TryPlace("p1", "s0"));
        }

        [Test]
        public void Sort_AllGeneratedRounds_AreSolvable()
        {
            var catalog = LevelCatalog.FromJson(File.ReadAllText(TestPaths.LevelsJson()), 100, 10);
            foreach (string type in new[] { GameTypes.ColorBox, GameTypes.ShapeSort, GameTypes.AnimalHabitat, GameTypes.Puzzle })
            {
                foreach (LevelConfig level in catalog.All.Where(l => l.GameType == type))
                {
                    var round = (SortRound)new RoundGenerator(new Random(level.LevelId)).GenerateOne(type, level);
                    var logic = new SortLogic(round);
                    foreach (SortItem item in round.Items) logic.TryPlace(item.Id, item.TargetBinId);
                    Assert.IsTrue(logic.IsDone, type + " bölüm " + level.LevelId);
                    Assert.AreEqual(0, logic.Mistakes);
                }
            }
        }

        // --- MazeNavigator ---

        [Test]
        public void Maze_SolvedByWalkingShortestPath()
        {
            for (int size = 3; size <= 7; size++)
            {
                MazeRound maze = MazeGenerator.Generate(size, size, new Random(size));
                var nav = new MazeNavigator(maze);
                int[] dist = MazeGenerator.Distances(maze.Walls, size, size, maze.GoalX, maze.GoalY);
                int guard = size * size * 4;
                while (!nav.AtGoal && guard-- > 0)
                {
                    foreach (Wall dir in new[] { Wall.North, Wall.East, Wall.South, Wall.West })
                    {
                        if (!MazeGenerator.CanMove(maze, nav.X, nav.Y, dir)) continue;
                        int nx = nav.X + (dir == Wall.East ? 1 : dir == Wall.West ? -1 : 0);
                        int ny = nav.Y + (dir == Wall.South ? 1 : dir == Wall.North ? -1 : 0);
                        if (dist[ny * size + nx] < dist[nav.Y * size + nav.X])
                        {
                            nav.Move(dir);
                            break;
                        }
                    }
                }
                Assert.IsTrue(nav.AtGoal, "Boyut " + size);
                Assert.AreEqual(0, nav.Bumps);
            }
        }

        [Test]
        public void Maze_WallBumps_AreForgiving()
        {
            MazeRound maze = MazeGenerator.Generate(4, 4, new Random(1));
            var nav = new MazeNavigator(maze);
            Wall blocked = new[] { Wall.North, Wall.West }.First(d => !MazeGenerator.CanMove(maze, 0, 0, d));
            Assert.AreEqual(MoveResult.Blocked, nav.Move(blocked));
            Assert.AreEqual(MoveResult.Blocked, nav.Move(blocked));
            Assert.AreEqual(0, nav.Mistakes); // 2 çarpma henüz hata değil
            nav.Move(blocked);
            Assert.AreEqual(1, nav.Mistakes);
            Assert.AreEqual(0, nav.X);
            Assert.AreEqual(0, nav.Y);
        }

        // --- LevelSession ---

        [Test]
        public void Session_PerfectPlay_ReachesTargetScore_AndThreeStars()
        {
            var catalog = LevelCatalog.FromJson(File.ReadAllText(TestPaths.LevelsJson()), 100, 10);
            LevelConfig level = catalog.Get(1);
            var session = new LevelSession(level, new RoundGenerator(new Random(1)).Generate(level), 10d);
            while (!session.IsFinished) session.CompleteRound(0);

            Assert.AreEqual(level.TargetScore, session.Score);
            Assert.AreEqual(3, StarCalculator.StarsForScore(level, session.Score));
            Assert.Throws<InvalidOperationException>(() => session.CompleteRound(0));
            Assert.AreEqual(2500, session.ElapsedMs(12.5d));
        }

        [Test]
        public void Session_WorstPlay_StillEarnsAStar_AndNeverNegative()
        {
            var catalog = LevelCatalog.FromJson(File.ReadAllText(TestPaths.LevelsJson()), 100, 10);
            foreach (LevelConfig level in catalog.All)
            {
                var session = new LevelSession(level, new RoundGenerator(new Random(2)).Generate(level), 0d);
                while (!session.IsFinished) session.CompleteRound(1000);
                Assert.GreaterOrEqual(session.Score, 0);
                Assert.GreaterOrEqual(StarCalculator.StarsForScore(level, session.Score), 1, "Bölüm " + level.LevelId);
            }
        }

        [Test]
        public void Session_RejectsEmptyRounds()
        {
            var catalog = LevelCatalog.FromJson(File.ReadAllText(TestPaths.LevelsJson()), 100, 10);
            Assert.Throws<ArgumentException>(() => new LevelSession(catalog.Get(1), new RoundSpec[0], 0d));
        }
    }
}
