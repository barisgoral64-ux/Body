using System.Collections.Generic;
using MinikDuello.Domain.Rounds;
using MinikDuello.Infra;
using UnityEngine;
using UnityEngine.UI;

namespace MinikDuello.UI
{
    /// <summary>Sürükle-bırak turları: renge/şekle/yaşam alanına göre ayır ve puzzle parçalarını yuvaya taşı.</summary>
    public sealed class SortRoundView : RoundView
    {
        private const float TrayCell = 190f;
        private const float BinSpacing = 20f;
        private const float BinHeight = 300f;
        private const float PlacedIcon = 84f;

        private sealed class BinUi
        {
            public SortBin Bin;
            public RectTransform Rect;
            public RectTransform Content;
            public GameObject VisualObject;
            public Image Background;
        }

        private readonly List<BinUi> bins = new List<BinUi>();
        private readonly Dictionary<string, DragItem> items = new Dictionary<string, DragItem>();
        private SortRound round;
        private SortLogic logic;
        private RectTransform tray;
        private DragItem selected;

        public override void Build(RoundSpec spec)
        {
            round = (SortRound)spec;
            logic = new SortLogic(round);

            int columns = Mathf.Min(4, Mathf.Max(2, round.Items.Count));
            int rows = Mathf.CeilToInt(round.Items.Count / (float)columns);
            tray = UIFactory.CreateGrid(transform, columns, new Vector2(TrayCell, TrayCell), new Vector2(20f, 20f), rows);
            foreach (SortItem item in round.Items) BuildItem(item);

            UIFactory.CreateSpacer(transform, 10f);
            BuildBins();
        }

        public override void ShowHint()
        {
            SortItem hint = logic.HintItem();
            if (hint == null) return;
            if (items.TryGetValue(hint.Id, out DragItem drag)) PulseOn(drag, true);
            foreach (BinUi b in bins)
            {
                if (b.Bin.Id == hint.TargetBinId) PulseOn(b.Rect, true);
            }
        }

        private void BuildItem(SortItem item)
        {
            var go = new GameObject("Item_" + item.Id, typeof(RectTransform), typeof(Image), typeof(DragItem));
            go.transform.SetParent(tray, false);
            var bg = go.GetComponent<Image>();
            bg.sprite = SpriteFactory.RoundedRect();
            bg.type = Image.Type.Sliced;
            bg.color = UITheme.Card;

            RectTransform visual = VisualFactory.Create(go.transform, item.Visual, TrayCell * 0.78f);
            visual.anchorMin = visual.anchorMax = new Vector2(0.5f, 0.5f);
            visual.anchoredPosition = Vector2.zero;

            var drag = go.GetComponent<DragItem>();
            drag.ItemId = item.Id;
            drag.Locked = () => InputBlocked;
            drag.DragLayer = Ctx.DragLayer;
            drag.Dropped = OnDropped;
            drag.Clicked = OnItemClicked;
            items[item.Id] = drag;
        }

        private void BuildBins()
        {
            int n = round.Bins.Count;
            float width = Mathf.Min(260f, (UITheme.ContentWidth - (n - 1) * BinSpacing) / n);
            float height = round.SingleSlot ? width : BinHeight;
            RectTransform row = UIFactory.CreateRow(transform, BinSpacing, height);

            foreach (SortBin bin in round.Bins)
            {
                var go = new GameObject("Bin_" + bin.Id, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                go.transform.SetParent(row, false);
                var bg = go.GetComponent<Image>();
                bg.sprite = SpriteFactory.RoundedRect();
                bg.type = Image.Type.Sliced;
                bg.color = new Color32(255, 255, 255, 235);
                var le = go.GetComponent<LayoutElement>();
                le.preferredWidth = width;
                le.preferredHeight = height;

                var ui = new BinUi { Bin = bin, Rect = (RectTransform)go.transform, Background = bg };
                float visualSize = round.SingleSlot ? width * 0.9f : width * 0.6f;
                RectTransform visual = VisualFactory.Create(go.transform, bin.Visual, visualSize);
                visual.anchorMin = visual.anchorMax = round.SingleSlot ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 1f);
                visual.pivot = round.SingleSlot ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 1f);
                visual.anchoredPosition = round.SingleSlot ? Vector2.zero : new Vector2(0f, -10f);
                ui.VisualObject = visual.gameObject;

                if (!round.SingleSlot)
                {
                    var contentGo = new GameObject("Placed", typeof(RectTransform), typeof(GridLayoutGroup));
                    contentGo.transform.SetParent(go.transform, false);
                    ui.Content = (RectTransform)contentGo.transform;
                    ui.Content.anchorMin = new Vector2(0f, 0f);
                    ui.Content.anchorMax = new Vector2(1f, 0.38f);
                    ui.Content.offsetMin = new Vector2(8f, 8f);
                    ui.Content.offsetMax = new Vector2(-8f, 0f);
                    var grid = contentGo.GetComponent<GridLayoutGroup>();
                    grid.cellSize = new Vector2(PlacedIcon * 0.7f, PlacedIcon * 0.7f);
                    grid.spacing = new Vector2(4f, 4f);
                    grid.childAlignment = TextAnchor.LowerCenter;
                }

                BinUi captured = ui;
                go.GetComponent<Button>().onClick.AddListener(() => OnBinClicked(captured));
                bins.Add(ui);
            }
        }

        private void OnItemClicked(DragItem item)
        {
            if (InputBlocked || logic.IsPlaced(item.ItemId)) return;
            if (selected != null) PulseOn(selected, false);
            selected = item == selected ? null : item;
            if (selected != null)
            {
                PulseOn(selected, true);
                Play(Sfx.Tap);
            }
        }

        private void OnBinClicked(BinUi bin)
        {
            if (InputBlocked || selected == null) return;
            DragItem item = selected;
            selected = null;
            PulseOn(item, false);
            Place(item, bin);
        }

        private void OnDropped(DragItem item, Vector2 screenPoint)
        {
            foreach (BinUi b in bins)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(b.Rect, screenPoint, null))
                {
                    Place(item, b);
                    return;
                }
            }
            item.ReturnHome();
        }

        private void Place(DragItem item, BinUi bin)
        {
            PlaceResult result = logic.TryPlace(item.ItemId, bin.Bin.Id);
            switch (result)
            {
                case PlaceResult.Ignored:
                    item.ReturnHome();
                    return;
                case PlaceResult.Wrong:
                    item.ReturnHome();
                    RegisterMistake(bin.Rect);
                    return;
            }

            Play(Sfx.Correct);
            SortItem data = round.Items.Find(i => i.Id == item.ItemId);
            PulseOn(item, false);
            Destroy(item.gameObject);
            items.Remove(item.ItemId);
            ShowPlaced(bin, data);
            if (Effects.Instance != null) Effects.Instance.Pop(bin.Rect);
            ClearHintPulses();

            if (result == PlaceResult.Completed)
            {
                Mistakes = logic.Mistakes;
                Finish();
            }
        }

        private void ShowPlaced(BinUi bin, SortItem item)
        {
            if (round.SingleSlot)
            {
                Destroy(bin.VisualObject);
                RectTransform visual = VisualFactory.Create(bin.Rect, item.Visual, bin.Rect.sizeDelta.x);
                UIFactory.Stretch(visual);
                bin.Background.color = new Color32(200, 235, 200, 255);
                return;
            }
            VisualFactory.Create(bin.Content, item.Visual, PlacedIcon);
        }

        private void ClearHintPulses()
        {
            foreach (BinUi b in bins) PulseOn(b.Rect, false);
            foreach (DragItem d in items.Values) PulseOn(d, false);
        }
    }
}
