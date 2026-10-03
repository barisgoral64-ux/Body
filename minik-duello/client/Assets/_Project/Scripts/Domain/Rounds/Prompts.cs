using System.Collections.Generic;

namespace MinikDuello.Domain.Rounds
{
    /// <summary>Türkçe adlar ve sesli yönerge metinleri. Ses klibi anahtarı = PromptKey (+ hedef), bkz. IVoiceService.</summary>
    public static class Names
    {
        private static readonly Dictionary<string, string> Colors = new Dictionary<string, string>
        {
            { "red", "Kırmızı" }, { "blue", "Mavi" }, { "green", "Yeşil" }, { "yellow", "Sarı" },
            { "orange", "Turuncu" }, { "purple", "Mor" }
        };

        private static readonly Dictionary<string, string> Shapes = new Dictionary<string, string>
        {
            { "circle", "Daire" }, { "square", "Kare" }, { "triangle", "Üçgen" },
            { "rectangle", "Dikdörtgen" }, { "star", "Yıldız" }, { "heart", "Kalp" }
        };

        private static readonly Dictionary<string, string> Animals = new Dictionary<string, string>
        {
            { "cat", "Kedi" }, { "dog", "Köpek" }, { "cow", "İnek" }, { "duck", "Ördek" }, { "sheep", "Koyun" },
            { "pig", "Domuz" }, { "rabbit", "Tavşan" }, { "fox", "Tilki" }, { "frog", "Kurbağa" }, { "bear", "Ayı" }
        };

        private static readonly Dictionary<string, string> Habitats = new Dictionary<string, string>
        {
            { "farm", "Çiftlik" }, { "pond", "Göl" }, { "forest", "Orman" }
        };

        private static readonly Dictionary<string, string> Symbols = new Dictionary<string, string>
        {
            { "apple", "Elma" }, { "ball", "Top" }, { "car", "Araba" }, { "fish", "Balık" },
            { "sun", "Güneş" }, { "moon", "Ay" }, { "balloon", "Balon" }
        };

        public static string Of(VisualKind kind, string key)
        {
            switch (kind)
            {
                case VisualKind.Color: return Lookup(Colors, key);
                case VisualKind.Shape: return Lookup(Shapes, key);
                case VisualKind.Animal: return Lookup(Animals, key);
                case VisualKind.Number: return key;
                case VisualKind.Symbol: return Lookup(Symbols, key, Lookup(Habitats, key));
                default: return key;
            }
        }

        public static string Color(string key) => Lookup(Colors, key);
        public static string Shape(string key) => Lookup(Shapes, key);
        public static string Animal(string key) => Lookup(Animals, key);

        private static string Lookup(Dictionary<string, string> map, string key, string fallback = null)
        {
            return map.TryGetValue(key, out string value) ? value : (fallback ?? key);
        }
    }

    public static class Prompts
    {
        /// <summary>Çocuğa gösterilen/okunan kısa yönerge.</summary>
        public static string Text(string promptKey, IDictionary<string, string> p)
        {
            string target = Get(p, "target");
            switch (promptKey)
            {
                case "findColor": return Names.Color(target) + " rengi bul!";
                case "findShape": return Names.Shape(target) + " bul!";
                case "findAnimal": return Names.Animal(target) + " nerede?";
                case "animalSound": return "Bu ses hangi hayvan?";
                case "countSelect": return "Kaç tane var?";
                case "missingColor": return "Eksik rengi bul!";
                case "pattern": return "Sıradaki hangisi?";
                case "matchColors": return "Aynı renkleri eşleştir!";
                case "matchShapes": return "Aynı şekilleri eşleştir!";
                case "matchAnimals": return "Aynı hayvanları eşleştir!";
                case "memoryPairs": return "Aynı kartları bul!";
                case "sortColors": return "Renklere göre ayır!";
                case "sortShapes": return "Şekillere göre ayır!";
                case "sortHabitats": return "Hayvanları evlerine götür!";
                case "puzzleAssemble": return "Puzzle'ı tamamla!";
                case "puzzleMissing": return "Eksik parçayı bul!";
                case "memoryPosition": return "Hangisiydi?";
                case "findStars": return "Yıldızları bul!";
                case "mazeGoal": return "Hazineye ulaş!";
                default: return string.Empty;
            }
        }

        private static string Get(IDictionary<string, string> p, string key)
        {
            return p != null && p.TryGetValue(key, out string v) ? v : string.Empty;
        }
    }
}
