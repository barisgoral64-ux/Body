using System.Collections.Generic;
using MinikDuello.Domain.Rounds;
using UnityEngine;
using UnityEngine.UI;

namespace MinikDuello.UI
{
    /// <summary>
    /// VisualRef → çizim. Şu an prosedürel yer tutucu çizimlerdir; sanatçı sprite'ları gelince
    /// yalnızca bu sınıf değişir (Resources/Art/{tür}/{anahtar} varsa onu kullanır).
    /// </summary>
    public static class VisualFactory
    {
        private const string ArtFolder = "Art/";

        public static readonly Dictionary<string, Color> ColorMap = new Dictionary<string, Color>
        {
            { "red", new Color32(229, 57, 53, 255) }, { "blue", new Color32(30, 136, 229, 255) },
            { "green", new Color32(67, 160, 71, 255) }, { "yellow", new Color32(253, 216, 53, 255) },
            { "orange", new Color32(251, 140, 0, 255) }, { "purple", new Color32(142, 36, 170, 255) }
        };

        /// <summary>Renk körlüğü için her renge ikinci bir ipucu: renk yanında ayırt edici şekil.</summary>
        private static readonly Dictionary<string, string> ColorCue = new Dictionary<string, string>
        {
            { "red", "heart" }, { "blue", "circle" }, { "green", "triangle" },
            { "yellow", "star" }, { "orange", "square" }, { "purple", "rectangle" }
        };

        private static readonly Dictionary<string, Color> ShapeTint = new Dictionary<string, Color>
        {
            { "circle", new Color32(66, 165, 245, 255) }, { "square", new Color32(102, 187, 106, 255) },
            { "triangle", new Color32(255, 112, 67, 255) }, { "rectangle", new Color32(171, 105, 230, 255) },
            { "star", new Color32(255, 202, 40, 255) }, { "heart", new Color32(240, 98, 146, 255) }
        };

        private static readonly Color[] PieceColors =
        {
            new Color32(239, 83, 80, 255), new Color32(66, 165, 245, 255), new Color32(102, 187, 106, 255),
            new Color32(255, 202, 40, 255), new Color32(171, 105, 230, 255), new Color32(255, 138, 101, 255)
        };

        public static RectTransform Create(Transform parent, VisualRef visual, float size)
        {
            var root = new GameObject("Visual_" + visual.Key, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(size, size);

            var art = Resources.Load<Sprite>(ArtFolder + visual.Kind + "/" + visual.Key);
            if (art != null)
            {
                Image image = UIFactory.CreateImage(root.transform, "Art", Color.white);
                image.sprite = art;
                image.preserveAspect = true;
                UIFactory.Stretch(image.rectTransform);
                return rect;
            }

            switch (visual.Kind)
            {
                case VisualKind.Color: DrawColor(rect, visual.Key, size); break;
                case VisualKind.Shape: DrawShape(rect, visual.Key); break;
                case VisualKind.Number: DrawTile(rect, UITheme.Card, visual.Key, Mathf.RoundToInt(size * 0.6f)); break;
                case VisualKind.Animal: DrawTile(rect, AnimalColor(visual.Key), Names.Of(visual.Kind, visual.Key), Mathf.RoundToInt(size * 0.2f)); break;
                case VisualKind.Symbol: DrawSymbol(rect, visual.Key, size); break;
                case VisualKind.Piece: DrawPiece(rect, visual.Key, size); break;
                default: DrawTile(rect, UITheme.Neutral, visual.Key, Mathf.RoundToInt(size * 0.5f)); break;
            }
            return rect;
        }

        public static Color ColorOf(string key) => ColorMap.TryGetValue(key, out Color c) ? c : UITheme.Neutral;

        private static void DrawColor(RectTransform rect, string key, float size)
        {
            Image swatch = UIFactory.CreatePanel(rect, "Swatch", ColorOf(key));
            UIFactory.Stretch(swatch.rectTransform);
            if (ColorCue.TryGetValue(key, out string cue))
            {
                Image icon = UIFactory.CreateImage(rect, "Cue", new Color(1f, 1f, 1f, 0.85f));
                icon.sprite = SpriteFactory.Shape(cue);
                icon.rectTransform.sizeDelta = new Vector2(size * 0.4f, size * 0.4f);
            }
        }

        private static void DrawShape(RectTransform rect, string key)
        {
            Image img = UIFactory.CreateImage(rect, "Shape", ShapeTint.TryGetValue(key, out Color tint) ? tint : UITheme.Neutral);
            img.sprite = SpriteFactory.Shape(key);
            img.preserveAspect = true;
            UIFactory.Stretch(img.rectTransform);
        }

        private static void DrawTile(RectTransform rect, Color color, string text, int fontSize)
        {
            Image tile = UIFactory.CreatePanel(rect, "Tile", color);
            UIFactory.Stretch(tile.rectTransform);
            Text label = UIFactory.CreateLabel(rect, text, fontSize, UITheme.TextDark, 0f);
            UIFactory.Stretch(label.rectTransform);
            Object.Destroy(label.GetComponent<LayoutElement>());
        }

        private static void DrawSymbol(RectTransform rect, string key, float size)
        {
            if (key == "balloon")
            {
                Image balloon = UIFactory.CreateImage(rect, "Balloon", new Color32(239, 83, 80, 255));
                balloon.sprite = SpriteFactory.Shape("circle");
                UIFactory.Stretch(balloon.rectTransform);
                return;
            }
            DrawTile(rect, new Color32(255, 224, 178, 255), Names.Of(VisualKind.Symbol, key), Mathf.RoundToInt(size * 0.2f));
        }

        private static void DrawPiece(RectTransform rect, string key, float size)
        {
            // Anahtar biçimi: {resim}_{indeks}[_slot]
            string[] parts = key.Split('_');
            int index = 0;
            if (parts.Length > 1) int.TryParse(parts[1], out index);
            bool slot = parts.Length > 2 && parts[2] == "slot";
            Color baseColor = PieceColors[Mathf.Abs(index) % PieceColors.Length];
            Image tile = UIFactory.CreatePanel(rect, "Piece", slot ? new Color(baseColor.r, baseColor.g, baseColor.b, 0.25f) : baseColor);
            UIFactory.Stretch(tile.rectTransform);
            Text number = UIFactory.CreateLabel(rect, (index + 1).ToString(), Mathf.RoundToInt(size * 0.45f), slot ? UITheme.Neutral : UITheme.TextLight, 0f);
            UIFactory.Stretch(number.rectTransform);
            Object.Destroy(number.GetComponent<LayoutElement>());
        }

        private static Color AnimalColor(string key)
        {
            int hash = 0;
            foreach (char ch in key) hash = hash * 31 + ch;
            return Color.Lerp(PieceColors[Mathf.Abs(hash) % PieceColors.Length], Color.white, 0.55f);
        }
    }
}
