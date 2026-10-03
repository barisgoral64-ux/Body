using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MinikDuello.UI
{
    /// <summary>
    /// Hafif animasyonlar: sallanma, pop, nabız, yıldız patlaması, konfeti. Parçacıklar havuzdan yeniden kullanılır;
    /// süreler kısadır (akışı yavaşlatmaz).
    /// </summary>
    public sealed class Effects : MonoBehaviour
    {
        private const float ShakeSeconds = 0.3f;
        private const float ShakeAmount = 18f;
        private const float PopSeconds = 0.25f;
        private const float BurstSeconds = 0.8f;
        private const int PoolSize = 36;

        private static readonly Color[] ConfettiColors =
        {
            new Color32(239, 83, 80, 255), new Color32(66, 165, 245, 255), new Color32(102, 187, 106, 255),
            new Color32(255, 202, 40, 255), new Color32(171, 105, 230, 255), new Color32(255, 138, 101, 255)
        };

        private readonly List<Image> pool = new List<Image>();
        private RectTransform layer;

        public static Effects Instance { get; private set; }

        public static Effects Create(Transform parent, RectTransform overlay)
        {
            var go = new GameObject("Effects", typeof(Effects));
            go.transform.SetParent(parent, false);
            var fx = go.GetComponent<Effects>();
            fx.layer = overlay;
            Instance = fx;
            return fx;
        }

        public void Shake(RectTransform target)
        {
            if (target != null) StartCoroutine(ShakeRoutine(target));
        }

        public void Pop(RectTransform target)
        {
            if (target != null) StartCoroutine(PopRoutine(target));
        }

        /// <summary>Hedef ekran noktasında yıldız/konfeti patlaması.</summary>
        public void Burst(Vector2 screenPoint, bool stars)
        {
            int count = stars ? 14 : PoolSize;
            for (int i = 0; i < count; i++)
            {
                Image p = Rent();
                p.sprite = SpriteFactory.Shape(stars ? "star" : (i % 2 == 0 ? "circle" : "square"));
                p.color = stars ? new Color32(255, 202, 40, 255) : ConfettiColors[i % ConfettiColors.Length];
                RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screenPoint, null, out Vector2 local);
                p.rectTransform.anchoredPosition = local;
                p.rectTransform.sizeDelta = Vector2.one * (stars ? 54f : 26f);
                p.gameObject.SetActive(true);
                StartCoroutine(BurstRoutine(p, Random.Range(0f, 360f), Random.Range(180f, 520f)));
            }
        }

        public void BurstAtCenter(bool stars) => Burst(new Vector2(Screen.width * 0.5f, Screen.height * 0.55f), stars);

        private Image Rent()
        {
            foreach (Image p in pool)
            {
                if (!p.gameObject.activeSelf) return p;
            }
            Image created = UIFactory.CreateImage(layer, "Particle", Color.white);
            pool.Add(created);
            return created;
        }

        private static IEnumerator ShakeRoutine(RectTransform target)
        {
            Vector2 origin = target.anchoredPosition;
            float t = 0f;
            while (t < ShakeSeconds && target != null)
            {
                t += Time.unscaledDeltaTime;
                float fade = 1f - t / ShakeSeconds;
                target.anchoredPosition = origin + new Vector2(Mathf.Sin(t * 60f) * ShakeAmount * fade, 0f);
                yield return null;
            }
            if (target != null) target.anchoredPosition = origin;
        }

        private static IEnumerator PopRoutine(RectTransform target)
        {
            float t = 0f;
            while (t < PopSeconds && target != null)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Sin(t / PopSeconds * Mathf.PI);
                target.localScale = Vector3.one * (1f + 0.25f * k);
                yield return null;
            }
            if (target != null) target.localScale = Vector3.one;
        }

        private IEnumerator BurstRoutine(Image p, float angleDeg, float distance)
        {
            RectTransform rt = p.rectTransform;
            Vector2 start = rt.anchoredPosition;
            Vector2 dir = new Vector2(Mathf.Cos(angleDeg * Mathf.Deg2Rad), Mathf.Sin(angleDeg * Mathf.Deg2Rad));
            float t = 0f;
            while (t < BurstSeconds)
            {
                t += Time.unscaledDeltaTime;
                float k = t / BurstSeconds;
                // Dışa doğru yavaşlayan hareket + hafif yer çekimi
                rt.anchoredPosition = start + dir * distance * (1f - (1f - k) * (1f - k)) + Vector2.down * 120f * k * k;
                rt.localRotation = Quaternion.Euler(0f, 0f, angleDeg + k * 180f);
                Color c = p.color;
                c.a = 1f - k;
                p.color = c;
                yield return null;
            }
            p.gameObject.SetActive(false);
        }
    }

    /// <summary>Sürekli nabız (ipucu vurgusu). Bileşen kaldırılınca ölçek sıfırlanır.</summary>
    public sealed class Pulse : MonoBehaviour
    {
        private const float Speed = 5f;
        private const float Amount = 0.1f;

        private void Update()
        {
            transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * Speed) * Amount);
        }

        private void OnDisable() => transform.localScale = Vector3.one;
    }
}
