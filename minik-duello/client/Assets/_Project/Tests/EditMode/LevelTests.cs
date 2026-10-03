using System;
using System.IO;
using System.Linq;
using MinikDuello.Domain.Levels;
using NUnit.Framework;

namespace MinikDuello.Tests
{
    public sealed class LevelTests
    {
        private const int Total = 100;
        private const int PerWorld = 10;
        private const double MaxStep = 0.05;

        private static LevelCatalog Load() => LevelCatalog.FromJson(File.ReadAllText(TestPaths.LevelsJson()), Total, PerWorld);

        [Test]
        public void ExportedCatalog_LoadsAndValidates()
        {
            LevelCatalog catalog = Load();
            Assert.AreEqual(Total, catalog.Count);
            Assert.AreEqual(1, catalog.Get(1).ColorCount > 0 ? 1 : 0);
            Assert.IsTrue(catalog.Get(1).Unlocked);
            Assert.IsFalse(catalog.Get(2).Unlocked);
            Assert.IsNull(catalog.Get(Total).NextLevelId);
        }

        [Test]
        public void Difficulty_NeverJumpsAbruptly()
        {
            LevelCatalog catalog = Load();
            for (int i = 2; i <= Total; i++)
            {
                double step = catalog.Get(i).Difficulty - catalog.Get(i - 1).Difficulty;
                Assert.LessOrEqual(step, MaxStep, "Bölüm " + i);
            }
        }

        [Test]
        public void WorldOne_StartsWithTwoColors()
        {
            LevelCatalog catalog = Load();
            Assert.AreEqual(2, catalog.Get(1).ColorCount);
            Assert.AreEqual(5, catalog.Get(10).ColorCount);
        }

        [Test]
        public void InvalidCatalog_IsRejected()
        {
            Assert.Throws<FormatException>(() => LevelCatalog.FromJson("{\"levels\":[]}", Total, PerWorld));
            string broken = File.ReadAllText(TestPaths.LevelsJson()).Replace("\"nextLevelID\": 2", "\"nextLevelID\": 5");
            Assert.Throws<FormatException>(() => LevelCatalog.FromJson(broken, Total, PerWorld));
        }

        [Test]
        public void Stars_FollowThresholds()
        {
            LevelConfig l = Load().Get(1);
            Assert.AreEqual(0, StarCalculator.StarsForScore(l, 0));
            Assert.AreEqual(1, StarCalculator.StarsForScore(l, l.StarRequirements[0]));
            Assert.AreEqual(2, StarCalculator.StarsForScore(l, l.StarRequirements[1]));
            Assert.AreEqual(3, StarCalculator.StarsForScore(l, l.TargetScore));
        }

        [Test]
        public void Difficulty_Adjust_OnlyEasesNeverHardens()
        {
            var manager = new DifficultyManager();
            LevelConfig level = Load().Get(25);

            DifficultyManager.Plan normal = manager.Adjust(level, 0);
            Assert.AreEqual(level.ObjectCount, normal.Level.ObjectCount);
            Assert.IsFalse(normal.HintsEnabled);

            for (int streak = 1; streak < 8; streak++)
            {
                DifficultyManager.Plan plan = manager.Adjust(level, streak);
                Assert.LessOrEqual(plan.Level.ObjectCount, level.ObjectCount);
                Assert.GreaterOrEqual(plan.Level.ObjectCount, DifficultyManager.MinObjectCount);
                Assert.LessOrEqual(plan.Level.TimeLimitSeconds, level.TimeLimitSeconds);
            }

            DifficultyManager.Plan struggling = manager.Adjust(level, DifficultyManager.StruggleThreshold);
            Assert.IsTrue(struggling.HintsEnabled);
            Assert.Less(struggling.Level.ObjectCount, level.ObjectCount);
            Assert.AreEqual(0, struggling.Level.TimeLimitSeconds);
            // Orijinal kataloğa dokunulmaz.
            Assert.AreEqual(Load().Get(25).ObjectCount, level.ObjectCount);
        }

        [Test]
        public void StruggleStreak_ResetsOnGoodResult()
        {
            Assert.AreEqual(1, DifficultyManager.NextStreak(0, 1));
            Assert.AreEqual(2, DifficultyManager.NextStreak(1, 0));
            Assert.AreEqual(0, DifficultyManager.NextStreak(5, 2));
        }

        [Test]
        public void EveryLevel_HasKnownGameType()
        {
            string[] known = typeof(GameTypes).GetFields().Select(f => (string)f.GetRawConstantValue()).ToArray();
            foreach (LevelConfig l in Load().All) CollectionAssert.Contains(known, l.GameType, "Bölüm " + l.LevelId);
        }
    }
}
