using System.Collections.Generic;
using Newtonsoft.Json;

namespace MinikDuello.Domain.Levels
{
    /// <summary>Oyun türü anahtarları (sunucu kataloğuyla birebir aynı).</summary>
    public static class GameTypes
    {
        public const string ColorMatch = "colorMatch";
        public const string ColorBox = "colorBox";
        public const string FindColor = "findColor";
        public const string MissingColor = "missingColor";
        public const string ShapeMatch = "shapeMatch";
        public const string FindShape = "findShape";
        public const string ShapeSort = "shapeSort";
        public const string CountSelect = "countSelect";
        public const string FindAnimal = "findAnimal";
        public const string AnimalSound = "animalSound";
        public const string AnimalHabitat = "animalHabitat";
        public const string AnimalPair = "animalPair";
        public const string MemoryCards = "memoryCards";
        public const string Maze = "maze";
        public const string QuickFind = "quickFind";
        public const string Pattern = "pattern";
        public const string Puzzle = "puzzle";
        public const string ExplorerMix = "explorerMix";
    }

    public sealed class LevelReward
    {
        [JsonProperty("coins")] public int Coins;
    }

    /// <summary>Bölüm tanımı. Veri tabanlıdır: yeni bölüm eklemek için kod değil levels.json değişir.</summary>
    public sealed class LevelConfig
    {
        [JsonProperty("levelID")] public int LevelId;
        [JsonProperty("worldID")] public int WorldId;
        [JsonProperty("gameType")] public string GameType;
        [JsonProperty("difficulty")] public double Difficulty;
        [JsonProperty("roundCount")] public int RoundCount;
        [JsonProperty("targetScore")] public int TargetScore;
        [JsonProperty("objectCount")] public int ObjectCount;
        [JsonProperty("colorCount")] public int ColorCount;
        [JsonProperty("timeLimitSeconds")] public int TimeLimitSeconds;
        [JsonProperty("starRequirements")] public int[] StarRequirements = new int[3];
        [JsonProperty("reward")] public LevelReward Reward = new LevelReward();
        [JsonProperty("unlocked")] public bool Unlocked;
        [JsonProperty("nextLevelID")] public int? NextLevelId;

        public LevelConfig Clone()
        {
            return new LevelConfig
            {
                LevelId = LevelId, WorldId = WorldId, GameType = GameType, Difficulty = Difficulty,
                RoundCount = RoundCount, TargetScore = TargetScore, ObjectCount = ObjectCount,
                ColorCount = ColorCount, TimeLimitSeconds = TimeLimitSeconds,
                StarRequirements = (int[])StarRequirements.Clone(),
                Reward = new LevelReward { Coins = Reward.Coins },
                Unlocked = Unlocked, NextLevelId = NextLevelId
            };
        }
    }

    public sealed class LevelFile
    {
        [JsonProperty("levels")] public List<LevelConfig> Levels = new List<LevelConfig>();
    }
}
