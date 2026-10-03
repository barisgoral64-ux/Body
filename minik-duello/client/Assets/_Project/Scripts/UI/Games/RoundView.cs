using System;
using MinikDuello.Domain.Rounds;
using MinikDuello.Infra;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MinikDuello.UI
{
    public sealed class RoundContext
    {
        public float TouchTarget;
        public bool Hints;
        /// <summary>Tur başı yönerge süresince true: dokunmalar yok sayılır.</summary>
        public Func<bool> Locked;
        public Action<int> Completed;
        /// <summary>Sürüklenen nesnelerin üstte çizildiği tam ekran katman.</summary>
        public RectTransform DragLayer;
        public AudioManager Audio;
    }

    /// <summary>Tur görünümü tabanı: dört etkileşim şablonu (seçmeli, eşleştirme, sürükle-bırak, labirent) bundan türer.</summary>
    public abstract class RoundView : MonoBehaviour
    {
        protected RoundContext Ctx { get; private set; }
        protected int Mistakes { get; set; }
        private bool finished;

        public void Init(RoundContext context)
        {
            Ctx = context;
            var layout = gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 30f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        public abstract void Build(RoundSpec spec);

        public virtual void ShowHint()
        {
        }

        protected bool InputBlocked => finished || (Ctx.Locked != null && Ctx.Locked());

        protected void Finish()
        {
            if (finished) return;
            finished = true;
            Ctx.Completed?.Invoke(Mistakes);
        }

        protected void RegisterMistake(RectTransform shake)
        {
            Mistakes++;
            Play(Sfx.Oops);
            if (Effects.Instance != null) Effects.Instance.Shake(shake);
            if (Ctx.Hints && Mistakes >= 2) ShowHint();
        }

        protected void Play(Sfx sfx)
        {
            if (Ctx.Audio != null) Ctx.Audio.Play(sfx);
        }

        protected static void PulseOn(Component target, bool on)
        {
            if (target == null) return;
            var pulse = target.GetComponent<Pulse>();
            if (on && pulse == null) target.gameObject.AddComponent<Pulse>();
            else if (!on && pulse != null) Destroy(pulse);
        }

        /// <summary>Beyaz yuvarlak köşeli kart düğmesi.</summary>
        protected static Button MakeCard(Transform parent, float size, Color color)
        {
            var go = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = SpriteFactory.RoundedRect();
            image.type = Image.Type.Sliced;
            image.color = color;
            ((RectTransform)go.transform).sizeDelta = new Vector2(size, size);
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            return button;
        }
    }

    public static class RoundViewFactory
    {
        public static RoundView Create(RoundSpec spec, Transform parent, RoundContext context)
        {
            var go = new GameObject("RoundView", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = UITheme.ContentWidth;
            element.flexibleHeight = 1f;

            RoundView view;
            switch (spec.Archetype)
            {
                case RoundArchetype.Choice: view = go.AddComponent<ChoiceRoundView>(); break;
                case RoundArchetype.Pairs: view = go.AddComponent<PairsRoundView>(); break;
                case RoundArchetype.Sort: view = go.AddComponent<SortRoundView>(); break;
                case RoundArchetype.Maze: view = go.AddComponent<MazeRoundView>(); break;
                default: throw new ArgumentOutOfRangeException(nameof(spec), "Bilinmeyen tur şablonu");
            }
            view.Init(context);
            view.Build(spec);
            return view;
        }
    }

    /// <summary>Sürüklenebilir nesne: sürükle-bırak ve dokun-seç ikisini de destekler (erişilebilirlik).</summary>
    public sealed class DragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        public string ItemId;
        public Func<bool> Locked;
        public RectTransform DragLayer;
        public Action<DragItem, Vector2> Dropped;
        public Action<DragItem> Clicked;

        private Transform originalParent;
        private int originalIndex;
        private bool dragging;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Locked != null && Locked()) return;
            dragging = true;
            originalParent = transform.parent;
            originalIndex = transform.GetSiblingIndex();
            transform.SetParent(DragLayer, false);
            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            transform.SetAsLastSibling();
            Follow(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragging) Follow(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging) return;
            dragging = false;
            Dropped?.Invoke(this, eventData.position);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!dragging) Clicked?.Invoke(this);
        }

        /// <summary>Yerleşemezse nesne ızgaradaki eski yerine döner.</summary>
        public void ReturnHome()
        {
            if (originalParent == null) return;
            transform.SetParent(originalParent, false);
            transform.SetSiblingIndex(originalIndex);
        }

        private void Follow(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(DragLayer, eventData.position, null, out Vector2 local);
            ((RectTransform)transform).anchoredPosition = local;
        }
    }

    /// <summary>Kaydırma hareketi algılayıcı (labirent).</summary>
    public sealed class SwipeArea : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const float MinSwipePixels = 40f;
        public Action<Vector2> Swiped;
        private Vector2 total;

        public void OnBeginDrag(PointerEventData eventData) => total = Vector2.zero;
        public void OnDrag(PointerEventData eventData) => total += eventData.delta;

        public void OnEndDrag(PointerEventData eventData)
        {
            if (total.magnitude >= MinSwipePixels) Swiped?.Invoke(total);
        }
    }
}
