using System;
using System.Collections.Generic;
using UnityEngine;

namespace MinikDuello.UI
{
    /// <summary>
    /// Prosedürel sprite üretimi (şekiller, yuvarlak köşeli dikdörtgen). Çizimler bir kez üretilip önbelleğe alınır
    /// (tek doku, düşük bellek). Sanatçı sprite'ları gelince VisualFactory bunların yerine geçirilir.
    /// </summary>
    public static class SpriteFactory
    {
        private const int Size = 128;
        private const int Samples = 3;
        private const float PixelsPerUnit = 100f;

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Shape(string name)
        {
            if (Cache.TryGetValue(name, out Sprite cached) && cached != null) return cached;
            Func<float, float, bool> inside = InsideFor(name);
            Sprite sprite = Render(inside);
            Cache[name] = sprite;
            return sprite;
        }

        /// <summary>9 dilimli (sliced) yuvarlak köşeli dikdörtgen; düğme ve kartlar için.</summary>
        public static Sprite RoundedRect()
        {
            const string key = "_rounded";
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            const float radius = 0.22f;
            Sprite sprite = Render((x, y) => InsideRounded(x, y, 0.02f, 0.98f, 0.02f, 0.98f, radius), true);
            Cache[key] = sprite;
            return sprite;
        }

        private static Func<float, float, bool> InsideFor(string name)
        {
            switch (name)
            {
                case "circle": return (x, y) => (x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f) <= 0.44f * 0.44f;
                case "square": return (x, y) => InsideRounded(x, y, 0.1f, 0.9f, 0.1f, 0.9f, 0.08f);
                case "rectangle": return (x, y) => InsideRounded(x, y, 0.04f, 0.96f, 0.25f, 0.75f, 0.06f);
                case "triangle": return (x, y) => InsideTriangle(x, y, 0.5f, 0.92f, 0.06f, 0.1f, 0.94f, 0.1f);
                case "star": return InsideStar;
                case "heart": return InsideHeart;
                default: return (x, y) => InsideRounded(x, y, 0.1f, 0.9f, 0.1f, 0.9f, 0.3f);
            }
        }

        private static bool InsideRounded(float x, float y, float x0, float x1, float y0, float y1, float r)
        {
            if (x < x0 || x > x1 || y < y0 || y > y1) return false;
            float cx = Mathf.Clamp(x, x0 + r, x1 - r);
            float cy = Mathf.Clamp(y, y0 + r, y1 - r);
            float dx = x - cx;
            float dy = y - cy;
            return dx * dx + dy * dy <= r * r;
        }

        private static bool InsideTriangle(float px, float py, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float d1 = Sign(px, py, ax, ay, bx, by);
            float d2 = Sign(px, py, bx, by, cx, cy);
            float d3 = Sign(px, py, cx, cy, ax, ay);
            bool hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
            bool hasPos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(hasNeg && hasPos);
        }

        private static float Sign(float px, float py, float ax, float ay, float bx, float by) =>
            (px - bx) * (ay - by) - (ax - bx) * (py - by);

        private static bool InsideStar(float x, float y)
        {
            float dx = x - 0.5f;
            float dy = y - 0.52f;
            float angle = Mathf.Atan2(dy, dx);
            float radius = Mathf.Sqrt(dx * dx + dy * dy);
            // Beş kollu yıldız: yarıçap açıya göre iç/dış değer arasında doğrusal değişir.
            const float outer = 0.46f;
            const float inner = 0.2f;
            float segment = 2f * Mathf.PI / 5f;
            float a = Mathf.Repeat(angle + Mathf.PI / 2f, segment);
            float t = Mathf.Abs(a - segment / 2f) / (segment / 2f);
            float limit = Mathf.Lerp(outer, inner, 1f - t);
            return radius <= limit;
        }

        private static bool InsideHeart(float x, float y)
        {
            float u = (x - 0.5f) * 2.6f;
            float v = (y - 0.45f) * 2.6f;
            float a = u * u + v * v - 1f;
            return a * a * a - u * u * v * v * v <= 0f;
        }

        private static Sprite Render(Func<float, float, bool> inside, bool sliced = false)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[Size * Size];
            for (int py = 0; py < Size; py++)
            {
                for (int px = 0; px < Size; px++)
                {
                    int covered = 0;
                    for (int sy = 0; sy < Samples; sy++)
                    {
                        for (int sx = 0; sx < Samples; sx++)
                        {
                            float x = (px + (sx + 0.5f) / Samples) / Size;
                            float y = (py + (sy + 0.5f) / Samples) / Size;
                            if (inside(x, y)) covered++;
                        }
                    }
                    byte alpha = (byte)(255 * covered / (Samples * Samples));
                    pixels[py * Size + px] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            Vector4 border = sliced ? new Vector4(Size * 0.3f, Size * 0.3f, Size * 0.3f, Size * 0.3f) : Vector4.zero;
            return Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect, border);
        }
    }
}
