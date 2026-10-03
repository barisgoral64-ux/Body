using System;
using System.Text;

namespace MinikDuello.Domain.Net
{
    /// <summary>
    /// Arkadaş kodu: HAYVAN-1234. Çocuk klavye kullanmaz; hayvanı seçer ve rakamlara dokunur (serbest metin yok).
    /// Biçim sunucuyla aynıdır (server/src/domain/identity.ts): rakamlar yalnızca 2-9.
    /// </summary>
    public static class FriendCodes
    {
        public const int DigitCount = 4;
        public const string AllowedDigits = "23456789";

        public static readonly string[] AnimalKeys = { "PANDA", "TAVSAN", "KEDI", "KOPEK", "DINO", "TILKI", "KOALA", "PENGUEN" };
        public static readonly string[] AnimalNames = { "Panda", "Tavşan", "Kedi", "Köpek", "Dino", "Tilki", "Koala", "Penguen" };

        public static string Format(string animalKey, string digits) => animalKey + "-" + digits;

        public static bool IsValid(string code)
        {
            if (string.IsNullOrEmpty(code)) return false;
            string[] parts = code.Split('-');
            if (parts.Length != 2 || Array.IndexOf(AnimalKeys, parts[0]) < 0 || parts[1].Length != DigitCount) return false;
            foreach (char c in parts[1])
            {
                if (AllowedDigits.IndexOf(c) < 0) return false;
            }
            return true;
        }

        /// <summary>Girilmekte olan kodun ekranda gösterimi (örn. "PANDA-48__").</summary>
        public static string Preview(string animalKey, string digits)
        {
            var sb = new StringBuilder(animalKey ?? "_____");
            sb.Append('-');
            sb.Append(digits ?? string.Empty);
            for (int i = (digits ?? string.Empty).Length; i < DigitCount; i++) sb.Append('_');
            return sb.ToString();
        }
    }
}
