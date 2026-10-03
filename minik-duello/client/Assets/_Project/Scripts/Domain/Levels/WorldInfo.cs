namespace MinikDuello.Domain.Levels
{
    /// <summary>Dünya adları (ekranda gösterilen).</summary>
    public static class WorldInfo
    {
        public const int Count = 10;

        private static readonly string[] Names =
        {
            "Renkler", "Şekiller", "Sayılar", "Hayvanlar", "Hafıza",
            "Labirent", "Hızlı Seçim", "Desenler", "Mini Puzzle", "Usta Kaşif"
        };

        public static string Name(int worldId) => worldId >= 1 && worldId <= Names.Length ? Names[worldId - 1] : "Dünya " + worldId;
    }
}
