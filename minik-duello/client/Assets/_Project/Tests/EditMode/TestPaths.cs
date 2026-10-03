using System;
using System.IO;

namespace MinikDuello.Tests
{
    internal static class TestPaths
    {
        /// <summary>Çalışma dizininden yukarı çıkarak Assets/_Project/Resources/levels.json dosyasını bulur (Unity ve .NET'te çalışır).</summary>
        public static string LevelsJson() => FindUp(Path.Combine("Assets", "_Project", "Resources", "levels.json"));

        /// <summary>minik-duello/protocol altındaki sunucu↔istemci fixture dosyaları.</summary>
        public static string Protocol(string file) => FindUp(Path.Combine("protocol", file), true);

        private static string FindUp(string relative, bool fromProjectRoot = false)
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 12 && dir != null; i++)
            {
                string candidate = Path.Combine(dir, relative);
                if (File.Exists(candidate)) return candidate;
                if (fromProjectRoot)
                {
                    // Unity çalışma dizini client/ olduğundan bir üst klasörde de aranır.
                    string up = Path.Combine(Path.GetDirectoryName(dir) ?? dir, relative);
                    if (File.Exists(up)) return up;
                }
                dir = Path.GetDirectoryName(dir);
            }
            throw new FileNotFoundException(relative + " bulunamadı.");
        }
    }
}
