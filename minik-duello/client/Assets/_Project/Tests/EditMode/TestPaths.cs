using System;
using System.IO;

namespace MinikDuello.Tests
{
    internal static class TestPaths
    {
        /// <summary>Çalışma dizininden yukarı çıkarak Assets/_Project/Resources/levels.json dosyasını bulur (Unity ve .NET'te çalışır).</summary>
        public static string LevelsJson()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 12 && dir != null; i++)
            {
                string candidate = Path.Combine(dir, "Assets", "_Project", "Resources", "levels.json");
                if (File.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            throw new FileNotFoundException("levels.json bulunamadı.");
        }
    }
}
