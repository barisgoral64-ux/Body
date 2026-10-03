using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MinikDuello.Domain.Levels
{
    /// <summary>Bölüm kataloğu: JSON'dan yüklenir ve bütünlüğü doğrulanır (ardışık kimlik, zincir, eşik sırası).</summary>
    public sealed class LevelCatalog
    {
        private readonly List<LevelConfig> levels;

        private LevelCatalog(List<LevelConfig> levels) => this.levels = levels;

        public int Count => levels.Count;
        public IReadOnlyList<LevelConfig> All => levels;

        public static LevelCatalog FromJson(string json, int expectedLevels, int levelsPerWorld)
        {
            LevelFile file = JsonConvert.DeserializeObject<LevelFile>(json);
            if (file == null || file.Levels == null) throw new FormatException("levels.json okunamadı.");
            if (file.Levels.Count != expectedLevels)
                throw new FormatException("Beklenen bölüm sayısı " + expectedLevels + ", bulunan " + file.Levels.Count);

            for (int i = 0; i < file.Levels.Count; i++)
            {
                LevelConfig l = file.Levels[i];
                if (l.LevelId != i + 1) throw new FormatException("Bölüm kimliği ardışık değil: " + l.LevelId);
                if (l.WorldId != i / levelsPerWorld + 1) throw new FormatException("Dünya hatalı: bölüm " + l.LevelId);
                if (l.StarRequirements == null || l.StarRequirements.Length != 3)
                    throw new FormatException("Yıldız eşikleri 3 olmalı: bölüm " + l.LevelId);
                if (l.StarRequirements[0] >= l.StarRequirements[1] || l.StarRequirements[1] > l.StarRequirements[2])
                    throw new FormatException("Yıldız eşikleri artan olmalı: bölüm " + l.LevelId);
                if (l.RoundCount <= 0 || l.ObjectCount <= 0) throw new FormatException("Geçersiz sayılar: bölüm " + l.LevelId);
                int? expectedNext = i < file.Levels.Count - 1 ? i + 2 : (int?)null;
                if (l.NextLevelId != expectedNext) throw new FormatException("Zincir hatalı: bölüm " + l.LevelId);
            }
            return new LevelCatalog(file.Levels);
        }

        public LevelConfig Get(int levelId)
        {
            if (levelId < 1 || levelId > levels.Count) throw new ArgumentOutOfRangeException(nameof(levelId));
            return levels[levelId - 1];
        }

        public bool Exists(int levelId) => levelId >= 1 && levelId <= levels.Count;

        public IEnumerable<LevelConfig> InWorld(int worldId)
        {
            foreach (LevelConfig l in levels)
            {
                if (l.WorldId == worldId) yield return l;
            }
        }
    }
}
