using System.Collections.Generic;

namespace MinikDuello.Domain.Rounds
{
    public enum VisualKind
    {
        Color,
        Shape,
        Number,
        Animal,
        Symbol,
        Piece,
        Unknown
    }

    /// <summary>Çizilecek nesnenin soyut tanımı. Gerçek çizimi UI katmanı yapar (sprite/ses sonradan değiştirilebilir).</summary>
    public readonly struct VisualRef
    {
        public readonly VisualKind Kind;
        public readonly string Key;

        public VisualRef(VisualKind kind, string key)
        {
            Kind = kind;
            Key = key;
        }

        public override string ToString() => Kind + ":" + Key;
    }

    public static class Content
    {
        public static readonly string[] Colors = { "red", "blue", "green", "yellow", "orange", "purple" };
        public static readonly string[] Shapes = { "circle", "square", "triangle", "rectangle", "star", "heart" };
        public static readonly string[] Symbols = { "apple", "ball", "car", "fish", "sun", "moon" };
        public static readonly string[] PuzzleImages = { "cat", "house", "tree", "boat" };
        public const int NumberMin = 1;
        public const int NumberMax = 10;

        public static readonly string[] Animals = { "cat", "dog", "cow", "duck", "sheep", "pig", "rabbit", "fox", "frog", "bear" };

        public static readonly Dictionary<string, string> AnimalHabitat = new Dictionary<string, string>
        {
            { "cow", "farm" }, { "sheep", "farm" }, { "pig", "farm" },
            { "duck", "pond" }, { "frog", "pond" }, { "fish", "pond" },
            { "rabbit", "forest" }, { "fox", "forest" }, { "bear", "forest" }
        };

        public static readonly string[] Habitats = { "farm", "pond", "forest" };

        /// <summary>Hayvan sesleri (görsel ipucu ve ses klibi anahtarı).</summary>
        public static readonly Dictionary<string, string> AnimalSound = new Dictionary<string, string>
        {
            { "cat", "Miyav!" }, { "dog", "Hav hav!" }, { "cow", "Möö!" }, { "duck", "Vak vak!" },
            { "sheep", "Meee!" }, { "pig", "Oink!" }, { "rabbit", "Tık tık!" }, { "fox", "Yip yip!" },
            { "frog", "Vrak!" }, { "bear", "Grrr!" }
        };

        public static readonly string[] SoundAnimals = { "cat", "dog", "cow", "duck", "sheep", "pig" };
        public static readonly string[] HabitatAnimals = { "cow", "sheep", "pig", "duck", "frog", "rabbit", "fox", "bear" };
    }
}
