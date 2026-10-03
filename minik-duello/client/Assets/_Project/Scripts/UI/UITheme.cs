using UnityEngine;

namespace MinikDuello.UI
{
    /// <summary>Renk ve ölçü sabitleri. Yüksek kontrast, yumuşak, korkutmayan tonlar.</summary>
    public static class UITheme
    {
        public static readonly Color Background = new Color32(255, 244, 214, 255);
        public static readonly Color Primary = new Color32(255, 140, 66, 255);
        public static readonly Color Green = new Color32(76, 175, 80, 255);
        public static readonly Color Blue = new Color32(66, 165, 245, 255);
        public static readonly Color Purple = new Color32(171, 105, 230, 255);
        public static readonly Color Pink = new Color32(240, 98, 146, 255);
        public static readonly Color Yellow = new Color32(255, 202, 40, 255);
        public static readonly Color Neutral = new Color32(120, 144, 156, 255);
        public static readonly Color Card = new Color32(255, 255, 255, 255);
        public static readonly Color CardDim = new Color32(230, 224, 210, 255);
        public static readonly Color Locked = new Color32(190, 184, 170, 255);
        public static readonly Color TextDark = new Color32(60, 40, 30, 255);
        public static readonly Color TextLight = Color.white;
        public static readonly Color Dim = new Color(0f, 0f, 0f, 0.55f);

        public const int ReferenceWidth = 1080;
        public const int ReferenceHeight = 1920;
        public const float MatchWidthOrHeight = 0.5f;
        public const float ButtonHeightFactor = 1.6f;
        public const float SmallButtonHeightFactor = 1.0f;
        public const float ButtonWidth = 760f;
        public const float Spacing = 28f;
        public const float Padding = 40f;
        public const int TitleFontSize = 76;
        public const int ButtonFontSize = 56;
        public const int BodyFontSize = 44;
        public const int SmallFontSize = 34;
        public const float TabletAspectThreshold = 1.45f;
        public const float ContentWidth = 960f;
    }
}
