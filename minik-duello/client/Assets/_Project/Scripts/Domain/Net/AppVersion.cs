using System;
using System.Text.RegularExpressions;

namespace MinikDuello.Domain.Net
{
    public sealed class AppVersionDto
    {
        public string MinSupportedVersion { get; set; }
        public string LatestVersion { get; set; }
        public string UpdateUrl { get; set; }
    }

    public enum UpdateStatus
    {
        UpToDate,
        /// <summary>Yeni sürüm var ama zorunlu değil.</summary>
        Available,
        /// <summary>Bu sürüm artık desteklenmiyor: güncellemeden devam edilemez.</summary>
        Required
    }

    /// <summary>Sürüm biçimi "1.2.3" (sonuna "-test" gibi ek olabilir, karşılaştırmada yok sayılır). Sunucuyla aynı kural.</summary>
    public static class AppVersion
    {
        private static readonly Regex Pattern = new Regex(@"^(\d{1,4})\.(\d{1,4})\.(\d{1,4})(?:[-+][0-9A-Za-z.\-]*)?$", RegexOptions.Compiled);

        public static bool TryParse(string input, out int[] parts)
        {
            parts = null;
            if (string.IsNullOrWhiteSpace(input)) return false;
            Match m = Pattern.Match(input.Trim());
            if (!m.Success) return false;
            parts = new[] { int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value) };
            return true;
        }

        /// <summary>a&lt;b → -1, eşit → 0, a&gt;b → 1. Geçersizse null.</summary>
        public static int? Compare(string a, string b)
        {
            if (!TryParse(a, out int[] pa) || !TryParse(b, out int[] pb)) return null;
            for (int i = 0; i < 3; i++)
            {
                if (pa[i] != pb[i]) return pa[i] < pb[i] ? -1 : 1;
            }
            return 0;
        }

        /// <summary>
        /// Karar kuralı: geçersiz/okunamayan sürüm bilgisi ASLA zorunlu güncelleme üretmez
        /// (çocuk yanlış bir yapılandırma yüzünden oyundan kilitlenmesin).
        /// </summary>
        public static UpdateStatus Evaluate(string current, AppVersionDto policy)
        {
            if (policy == null) return UpdateStatus.UpToDate;
            if (Compare(current, policy.MinSupportedVersion) == -1) return UpdateStatus.Required;
            if (Compare(current, policy.LatestVersion) == -1) return UpdateStatus.Available;
            return UpdateStatus.UpToDate;
        }
    }
}
