using System.Collections;
using MinikDuello.Core;
using MinikDuello.Domain.Net;
using MinikDuello.Services.Realtime;
using UnityEngine;
using UnityEngine.UI;

namespace MinikDuello.UI
{
    /// <summary>
    /// Hazır mesaj çubuğu: serbest metin YOK, yalnızca 6 sabit ifade gönderilebilir. Gelen mesaj kısa süre balon olarak görünür.
    /// </summary>
    public sealed class QuickChatBar : MonoBehaviour
    {
        private const float BubbleSeconds = 3f;

        private MatchSession match;
        private Text bubble;
        private Coroutine hideRoutine;

        public static QuickChatBar Create(Transform parent, float touch, MatchSession session)
        {
            var go = new GameObject("QuickChat", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement), typeof(QuickChatBar));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredWidth = UITheme.ContentWidth;
            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var bar = go.GetComponent<QuickChatBar>();
            bar.match = session;
            bar.Build(touch);
            return bar;
        }

        private void Build(float touch)
        {
            bubble = UIFactory.CreateLabel(transform, string.Empty, UITheme.BodyFontSize - 4, UITheme.Primary, UITheme.ContentWidth);
            bubble.gameObject.SetActive(false);

            RectTransform grid = UIFactory.CreateGrid(transform, 3, new Vector2(310f, Mathf.Max(90f, touch * 0.8f)), new Vector2(14f, 14f), 2);
            foreach (string id in QuickChats.Ids)
            {
                string captured = id;
                UIFactory.CreateButton(grid, QuickChats.Label(id), UITheme.Purple, 80f, true, () => match.SendQuickChat(captured));
            }
        }

        private void OnEnable()
        {
            if (match != null) match.QuickChatReceived += OnReceived;
        }

        private void OnDisable()
        {
            if (match != null) match.QuickChatReceived -= OnReceived;
            if (bubble != null) bubble.gameObject.SetActive(false);
        }

        private void OnReceived(QuickChatDto message)
        {
            if (!QuickChats.IsValid(message.Message)) return; // beklenmeyen içerik gösterilmez
            bubble.text = "Arkadaşın: " + QuickChats.Label(message.Message);
            bubble.gameObject.SetActive(true);
            if (hideRoutine != null) StopCoroutine(hideRoutine);
            hideRoutine = StartCoroutine(HideLater());
        }

        private IEnumerator HideLater()
        {
            yield return new WaitForSecondsRealtime(BubbleSeconds);
            bubble.gameObject.SetActive(false);
        }
    }
}
