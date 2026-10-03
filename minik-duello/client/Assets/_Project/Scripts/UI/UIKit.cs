using MinikDuello.Domain.Rewards;
using UnityEngine;
using UnityEngine.UI;

namespace MinikDuello.UI
{
    /// <summary>Ortak küçük bileşenler: yıldız satırı, avatar, istatistik rozeti.</summary>
    public static class UIKit
    {
        private static readonly Color Empty = new Color32(210, 204, 190, 255);

        public static RectTransform StarsRow(Transform parent, int filled, int total, float size)
        {
            RectTransform row = UIFactory.CreateRow(parent, size * 0.15f, size);
            row.GetComponent<LayoutElement>().preferredWidth = total * size * 1.15f;
            for (int i = 0; i < total; i++)
            {
                Image star = UIFactory.CreateImage(row, "Star", i < filled ? UITheme.Yellow : Empty);
                star.sprite = SpriteFactory.Shape("star");
                var le = star.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = size;
                le.preferredHeight = size;
            }
            return row;
        }

        public static Text Chip(Transform parent, string text, Color color, float width = 300f, int fontSize = UITheme.SmallFontSize + 4)
        {
            Image chip = UIFactory.CreatePanel(parent, "Chip", color);
            var le = chip.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = fontSize * 1.9f;
            Text label = UIFactory.CreateLabel(chip.transform, text, fontSize, UITheme.TextDark, 0f);
            UIFactory.Stretch(label.rectTransform);
            Object.Destroy(label.GetComponent<LayoutElement>());
            return label;
        }

        /// <summary>Çizilmiş ikon + metin rozeti (yazı tipindeki özel simgelere bağımlı değildir).</summary>
        public static Text IconChip(Transform parent, string shape, Color iconColor, string text, Color background, float width = 230f, int fontSize = UITheme.SmallFontSize + 4)
        {
            Image chip = UIFactory.CreatePanel(parent, "IconChip", background);
            var le = chip.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = fontSize * 1.9f;
            var layout = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 6, 6);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            Image icon = UIFactory.CreateImage(chip.transform, "Icon", iconColor);
            icon.sprite = SpriteFactory.Shape(shape);
            var iconLe = icon.gameObject.AddComponent<LayoutElement>();
            iconLe.preferredWidth = fontSize * 1.1f;
            iconLe.preferredHeight = fontSize * 1.1f;

            Text label = UIFactory.CreateLabel(chip.transform, text, fontSize, UITheme.TextDark, 0f);
            var labelLe = label.GetComponent<LayoutElement>();
            labelLe.preferredWidth = -1f;
            labelLe.flexibleWidth = 1f;
            return label;
        }

        public static Text StarChip(Transform parent, string text, float width = 230f, int fontSize = UITheme.SmallFontSize + 4) =>
            IconChip(parent, "star", UITheme.Yellow, text, new Color32(255, 243, 200, 255), width, fontSize);

        public static Text CoinChip(Transform parent, string text, float width = 230f, int fontSize = UITheme.SmallFontSize + 4) =>
            IconChip(parent, "circle", new Color32(255, 160, 0, 255), text, new Color32(255, 230, 190, 255), width, fontSize);

        public static Color CharacterColor(string character)
        {
            switch (character)
            {
                case "panda": return new Color32(245, 245, 245, 255);
                case "rabbit": return new Color32(255, 224, 235, 255);
                case "cat": return new Color32(255, 183, 77, 255);
                case "dog": return new Color32(188, 143, 100, 255);
                case "dinosaur": return new Color32(129, 199, 132, 255);
                case "fox": return new Color32(255, 138, 80, 255);
                case "koala": return new Color32(176, 190, 197, 255);
                case "penguin": return new Color32(120, 144, 190, 255);
                default: return UITheme.Neutral;
            }
        }

        /// <summary>
        /// Yer tutucu karakter çizimi (yüz + kulaklar + kuşanılan kozmetikler). Sanatçı sprite'ı gelince yalnızca bu yöntem değişir.
        /// </summary>
        public static RectTransform Avatar(Transform parent, string character, System.Collections.Generic.IDictionary<string, string> equipped, float size)
        {
            var root = new GameObject("Avatar_" + character, typeof(RectTransform), typeof(LayoutElement));
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(size, size);
            var le = root.GetComponent<LayoutElement>();
            le.preferredWidth = size;
            le.preferredHeight = size;

            var art = Resources.Load<Sprite>("Art/Character/" + character);
            if (art != null)
            {
                Image img = UIFactory.CreateImage(rect, "Art", Color.white);
                img.sprite = art;
                img.preserveAspect = true;
                UIFactory.Stretch(img.rectTransform);
                return rect;
            }

            Color body = CharacterColor(character);
            Color earColor = character == "panda" ? new Color32(40, 40, 40, 255) : Color.Lerp(body, Color.black, 0.2f);
            Part(rect, "circle", earColor, new Vector2(-0.3f, 0.34f), 0.28f, size);
            Part(rect, "circle", earColor, new Vector2(0.3f, 0.34f), 0.28f, size);
            Part(rect, "circle", body, new Vector2(0f, -0.02f), 0.86f, size);
            Part(rect, "circle", new Color32(40, 40, 40, 255), new Vector2(-0.17f, 0.06f), 0.11f, size);
            Part(rect, "circle", new Color32(40, 40, 40, 255), new Vector2(0.17f, 0.06f), 0.11f, size);
            Part(rect, "heart", new Color32(220, 100, 110, 255), new Vector2(0f, -0.16f), 0.14f, size);

            if (equipped != null)
            {
                if (equipped.ContainsKey("hat")) Part(rect, "triangle", equipped["hat"] == "hat_crown" ? UITheme.Yellow : UITheme.Pink, new Vector2(0f, 0.5f), 0.34f, size);
                if (equipped.ContainsKey("glasses"))
                {
                    Color g = new Color32(50, 50, 60, 255);
                    Part(rect, "circle", new Color(g.r, g.g, g.b, 0.45f), new Vector2(-0.17f, 0.06f), 0.24f, size);
                    Part(rect, "circle", new Color(g.r, g.g, g.b, 0.45f), new Vector2(0.17f, 0.06f), 0.24f, size);
                }
                if (equipped.ContainsKey("outfit")) Part(rect, "square", UITheme.Blue, new Vector2(0f, -0.5f), 0.36f, size);
                if (equipped.ContainsKey("shoes")) Part(rect, "rectangle", UITheme.Green, new Vector2(0f, -0.62f), 0.4f, size);
                if (equipped.ContainsKey("backpack")) Part(rect, "square", UITheme.Purple, new Vector2(0.52f, -0.1f), 0.22f, size);
                if (equipped.ContainsKey("effect")) Part(rect, "star", UITheme.Yellow, new Vector2(-0.5f, 0.5f), 0.2f, size);
            }
            return rect;
        }

        private static void Part(RectTransform parent, string shape, Color color, Vector2 offset, float scale, float size)
        {
            Image img = UIFactory.CreateImage(parent, shape, color);
            img.sprite = SpriteFactory.Shape(shape);
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            img.rectTransform.sizeDelta = Vector2.one * size * scale;
            img.rectTransform.anchoredPosition = offset * size;
        }

        public static string StarText(int value) => value.ToString();
    }
}
