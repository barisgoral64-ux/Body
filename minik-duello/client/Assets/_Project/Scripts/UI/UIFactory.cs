using MinikDuello.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MinikDuello.UI
{
    public sealed class CanvasLayers
    {
        public RectTransform Root;
        /// <summary>Çentik/köşe güvenli alanı: ekranlar burada.</summary>
        public RectTransform Safe;
        /// <summary>Tam ekran katman: açılır pencere, yükleme, bildirim.</summary>
        public RectTransform Overlay;
    }

    /// <summary>Kodla UI üretimi: elle sahne yapılandırması gerektirmez, tüm ekranlar aynı görünümü paylaşır.</summary>
    public static class UIFactory
    {
        private static Font cachedFont;

        public static Font DefaultFont
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

        public static CanvasLayers CreateCanvas(Transform parent)
        {
            var canvasObject = new GameObject("UICanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(UITheme.ReferenceWidth, UITheme.ReferenceHeight);
            scaler.matchWidthOrHeight = UITheme.MatchWidthOrHeight;

            Image background = CreateImage(canvasObject.transform, "Background", UITheme.Background);
            Stretch(background.rectTransform);

            var safe = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeAreaFitter));
            safe.transform.SetParent(canvasObject.transform, false);

            var overlay = new GameObject("Overlay", typeof(RectTransform));
            overlay.transform.SetParent(canvasObject.transform, false);
            Stretch((RectTransform)overlay.transform);

            return new CanvasLayers { Root = (RectTransform)canvasObject.transform, Safe = (RectTransform)safe.transform, Overlay = (RectTransform)overlay.transform };
        }

        public static RectTransform CreateScreenRoot(Transform parent, string name)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            Stretch(rect);

            var layout = root.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = UITheme.Spacing;
            int padding = Mathf.RoundToInt(UITheme.Padding);
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return rect;
        }

        public static Text CreateLabel(Transform parent, string text, int fontSize, Color color, float preferredWidth = UITheme.ButtonWidth, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var label = go.GetComponent<Text>();
            label.font = DefaultFont;
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;

            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = preferredWidth;
            // Yükseklik verilmez: Text kendi yerleşim yüksekliğini sarılan satır sayısına göre hesaplar (uzun metin taşmaz).
            return label;
        }

        public static Button CreateButton(Transform parent, string text, Color color, float minTouchTarget, bool small, UnityAction onClick)
        {
            var go = new GameObject("Button_" + text, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.sprite = SpriteFactory.RoundedRect();
            image.type = Image.Type.Sliced;
            image.color = color;

            float factor = small ? UITheme.SmallButtonHeightFactor : UITheme.ButtonHeightFactor;
            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = small ? Mathf.Max(minTouchTarget * 2f, 260f) : UITheme.ButtonWidth;
            element.preferredHeight = Mathf.Max(minTouchTarget, minTouchTarget * factor);
            element.minHeight = minTouchTarget;
            element.minWidth = minTouchTarget;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.95f, 0.95f, 0.95f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.6f);
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(onClick);

            int fontSize = small ? UITheme.SmallFontSize + 6 : UITheme.ButtonFontSize;
            Text label = CreateLabel(go.transform, text, fontSize, UITheme.TextLight);
            // Uzun yazılar düğmeye sığacak şekilde küçülür.
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 18;
            label.resizeTextMaxSize = fontSize;
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

        public static Image CreatePanel(Transform parent, string name, Color color, bool raycast = false)
        {
            Image image = CreateImage(parent, name, color);
            image.sprite = SpriteFactory.RoundedRect();
            image.type = Image.Type.Sliced;
            image.raycastTarget = raycast;
            return image;
        }

        public static RectTransform CreateRow(Transform parent, float spacing, float height = -1f, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = align;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = UITheme.ContentWidth;
            if (height > 0) element.preferredHeight = height;
            return (RectTransform)go.transform;
        }

        public static RectTransform CreateColumn(Transform parent, float spacing, float width = UITheme.ContentWidth)
        {
            var go = new GameObject("Column", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            go.GetComponent<LayoutElement>().preferredWidth = width;
            return (RectTransform)go.transform;
        }

        public static RectTransform CreateGrid(Transform parent, int columns, Vector2 cell, Vector2 spacing, int rows = 0)
        {
            var go = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var grid = go.GetComponent<GridLayoutGroup>();
            grid.cellSize = cell;
            grid.spacing = spacing;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.childAlignment = TextAnchor.UpperCenter;
            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = columns * cell.x + (columns - 1) * spacing.x;
            if (rows > 0) element.preferredHeight = rows * cell.y + (rows - 1) * spacing.y;
            return (RectTransform)go.transform;
        }

        /// <summary>Dikey kaydırmalı liste; içerik düğümünü döndürür. Yükseklik verilmezse kalan alanı doldurur.</summary>
        public static RectTransform CreateScroll(Transform parent, float preferredHeight = -1f)
        {
            var go = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var bg = go.GetComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.001f); // dokunma almak için
            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = UITheme.ContentWidth;
            if (preferredHeight > 0) element.preferredHeight = preferredHeight;
            else element.flexibleHeight = 1f;

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(go.transform, false);
            var rect = (RectTransform)content.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = UITheme.Spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = go.GetComponent<ScrollRect>();
            scroll.content = rect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;
            return rect;
        }

        public static void CreateSpacer(Transform parent, float height)
        {
            var go = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = height;
        }

        public static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                child.gameObject.SetActive(false); // aynı karede yerleşimden çıkar
                Object.Destroy(child.gameObject);
            }
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static float MinTouchTarget(AppSettings config)
        {
            float aspect = Screen.width > Screen.height
                ? (float)Screen.width / Mathf.Max(1, Screen.height)
                : (float)Screen.height / Mathf.Max(1, Screen.width);
            // Tablet oranları (4:3 gibi) telefon oranlarından daha kareye yakındır.
            return aspect < UITheme.TabletAspectThreshold ? config.MinTouchTargetTablet : config.MinTouchTargetPhone;
        }
    }
}
