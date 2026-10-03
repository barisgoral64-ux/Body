using System.Collections.Generic;
using MinikDuello.Domain.Rounds;
using MinikDuello.Infra;
using UnityEngine;
using UnityEngine.UI;

namespace MinikDuello.UI
{
    /// <summary>Tek doğru seçenekli turlar: renk/şekil/hayvan bul, say, deseni tamamla, eksik rengi bul, hızlı seç.</summary>
    public sealed class ChoiceRoundView : RoundView
    {
        private const float SequenceMaxItem = 150f;
        private const float CountItem = 120f;
        private const int CountColumns = 5;
        private const float OptionSpacing = 28f;

        private readonly Dictionary<string, Button> buttons = new Dictionary<string, Button>();
        private ChoiceRound round;

        public override void Build(RoundSpec spec)
        {
            round = (ChoiceRound)spec;
            if (round.Sequence.Count > 0) BuildSequence();
            if (round.CountToShow > 0) BuildCount();
            if (!string.IsNullOrEmpty(round.SoundText)) BuildSound();
            BuildOptions();
        }

        public override void ShowHint()
        {
            if (buttons.TryGetValue(round.CorrectId, out Button correct)) PulseOn(correct, true);
        }

        private void BuildSequence()
        {
            int n = round.Sequence.Count;
            float size = Mathf.Min(SequenceMaxItem, (UITheme.ContentWidth - (n - 1) * 16f) / n);
            RectTransform row = UIFactory.CreateRow(transform, 16f, size + 10f);
            foreach (VisualRef v in round.Sequence)
            {
                RectTransform visual = VisualFactory.Create(row, v, size);
                var le = visual.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = size;
                le.preferredHeight = size;
            }
        }

        private void BuildCount()
        {
            int rows = Mathf.CeilToInt(round.CountToShow / (float)CountColumns);
            RectTransform grid = UIFactory.CreateGrid(transform, CountColumns, new Vector2(CountItem, CountItem), new Vector2(20f, 20f), rows);
            for (int i = 0; i < round.CountToShow; i++) VisualFactory.Create(grid, round.CountVisual, CountItem);
        }

        private void BuildSound()
        {
            Image bubble = UIFactory.CreatePanel(transform, "SoundBubble", UITheme.Card);
            var le = bubble.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 700f;
            le.preferredHeight = 170f;
            Text text = UIFactory.CreateLabel(bubble.transform, round.SoundText, UITheme.TitleFontSize, UITheme.Primary, 0f);
            UIFactory.Stretch(text.rectTransform);
            Destroy(text.GetComponent<LayoutElement>());
        }

        private void BuildOptions()
        {
            int n = round.Options.Count;
            int columns = round.Grid ? 3 : (n <= 4 ? 2 : 3);
            float cell = round.Grid ? 280f : (columns == 2 ? 420f : 300f);
            int rows = Mathf.CeilToInt(n / (float)columns);
            RectTransform grid = UIFactory.CreateGrid(transform, columns, new Vector2(cell, cell), new Vector2(OptionSpacing, OptionSpacing), rows);

            foreach (ChoiceOption option in round.Options)
            {
                ChoiceOption captured = option;
                Button card = MakeCard(grid, cell, UITheme.Card);
                card.transform.SetParent(grid, false);
                RectTransform visual = VisualFactory.Create(card.transform, option.Visual, cell * 0.78f);
                visual.anchorMin = visual.anchorMax = new Vector2(0.5f, 0.5f);
                visual.anchoredPosition = Vector2.zero;
                card.onClick.AddListener(() => OnPick(captured, card));
                buttons[option.Id] = card;
            }
        }

        private void OnPick(ChoiceOption option, Button button)
        {
            if (InputBlocked) return;
            var rect = (RectTransform)button.transform;
            if (option.Id == round.CorrectId)
            {
                Play(Sfx.Correct);
                if (Effects.Instance != null) Effects.Instance.Pop(rect);
                foreach (Button b in buttons.Values) b.interactable = false;
                PulseOn(button, false);
                Finish();
                return;
            }
            button.interactable = false; // yanlış seçenek elenir: tekrar denemek kolaylaşır
            RegisterMistake(rect);
        }
    }
}
