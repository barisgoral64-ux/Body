using MinikDuello.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MinikDuello.UI
{
    /// <summary>Kodla UI üretimi: elle sahne yapılandırması gerektirmez, tüm ekranlar aynı görünümü paylaşır.</summary>
    public static class UIFactory
    {
        private static Font cachedFont;

        private static Font DefaultFont
        {
            get
            {
                if (cachedFont == null) cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return cachedFont;
            }
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        /// <summary>Canvas + güvenli alan kökü döndürür.</summary>
        public static RectTransform CreateCanvasWithSafeArea(Transform parent)
        {
            var canvasObject = new GameObject("UICanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(UITheme.ReferenceWidth, UITheme.ReferenceHeight);
            scaler.matchWidthOrHeight = UITheme.MatchWidthOrHeight;

            var background = CreateImage(canvasObject.transform, "Background", UITheme.Background);
            Stretch(background.rectTransform);

            var safe = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeAreaFitter));
            safe.transform.SetParent(canvasObject.transform, false);
            return (RectTransform)safe.transform;
        }

        public static RectTransform CreateScreenRoot(Transform parent, string name)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            Stretch(rect);

            var layout = root.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = UITheme.Spacing;
            int padding = Mathf.RoundToInt(UITheme.Padding);
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return rect;
        }

        public static Text CreateLabel(Transform parent, string text, int fontSize, Color color)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var label = go.GetComponent<Text>();
            label.font = DefaultFont;
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;

            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = UITheme.ButtonWidth;
            element.preferredHeight = fontSize * 1.6f;
            return label;
        }

        public static Button CreateButton(
            Transform parent,
            string text,
            Color color,
            float minTouchTarget,
            bool small,
            UnityAction onClick)
        {
            var go = new GameObject("Button_" + text, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = color;

            float factor = small ? UITheme.SmallButtonHeightFactor : UITheme.ButtonHeightFactor;
            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = small ? minTouchTarget * 1.5f : UITheme.ButtonWidth;
            element.preferredHeight = Mathf.Max(minTouchTarget, minTouchTarget * factor);
            element.minHeight = minTouchTarget;
            element.minWidth = minTouchTarget;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null) button.onClick.AddListener(onClick);

            var label = CreateLabel(go.transform, text, UITheme.ButtonFontSize, UITheme.TextLight);
            Stretch(label.rectTransform);
            Object.Destroy(label.GetComponent<LayoutElement>());
            return button;
        }

        public static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static float MinTouchTarget(GameConfig config)
        {
            float aspect = Screen.width > Screen.height
                ? (float)Screen.width / Mathf.Max(1, Screen.height)
                : (float)Screen.height / Mathf.Max(1, Screen.width);
            // Tablet oranları (4:3 gibi) telefon oranlarından daha kareye yakındır.
            return aspect < UITheme.TabletAspectThreshold ? config.minTouchTargetTablet : config.minTouchTargetPhone;
        }
    }
}
