using MinikDuello.Core;
using MinikDuello.ParentControls;
using UnityEngine;
using UnityEngine.UI;

namespace MinikDuello.UI
{
    /// <summary>Yetişkin kapısı: toplama sorusu + büyük rakam tuşları.</summary>
    public sealed class ParentGateScreen : ScreenBase
    {
        private const int Columns = 3;
        private const float CellWidth = 200f;
        private const float MinCellHeight = 150f;
        private const float CellSpacing = 20f;
        private const int Rows = 4;

        private readonly System.Random random = new System.Random();
        private ParentGateSession session;
        private Text questionLabel;
        private Text entryLabel;
        private Text feedbackLabel;
        private string entry = string.Empty;

        protected override void Build()
        {
            UIFactory.CreateLabel(transform, Strings.ParentGateTitle, UITheme.TitleFontSize, UITheme.TextDark);
            questionLabel = UIFactory.CreateLabel(transform, string.Empty, UITheme.TitleFontSize, UITheme.Primary);
            entryLabel = UIFactory.CreateLabel(transform, string.Empty, UITheme.TitleFontSize, UITheme.TextDark);
            feedbackLabel = UIFactory.CreateLabel(transform, string.Empty, UITheme.BodyFontSize, UITheme.TextDark);
            BuildNumberPad();
            UIFactory.CreateButton(transform, Strings.Back, UITheme.Neutral, TouchTarget, true, Ui.Back);
        }

        protected override void OnShown()
        {
            session = new ParentGateSession(random);
            entry = string.Empty;
            feedbackLabel.text = string.Empty;
            Refresh();
        }

        private void BuildNumberPad()
        {
            float cellHeight = Mathf.Max(MinCellHeight, TouchTarget);
            var pad = new GameObject("NumberPad", typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement));
            pad.transform.SetParent(transform, false);

            var grid = pad.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(CellWidth, cellHeight);
            grid.spacing = new Vector2(CellSpacing, CellSpacing);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Columns;
            grid.childAlignment = TextAnchor.MiddleCenter;

            var element = pad.GetComponent<LayoutElement>();
            element.preferredWidth = Columns * CellWidth + (Columns - 1) * CellSpacing;
            element.preferredHeight = Rows * cellHeight + (Rows - 1) * CellSpacing;

            for (int digit = 1; digit <= 9; digit++)
            {
                int captured = digit;
                UIFactory.CreateButton(pad.transform, captured.ToString(), UITheme.Blue, TouchTarget, true, () => AppendDigit(captured));
            }
            UIFactory.CreateButton(pad.transform, Strings.Erase, UITheme.Pink, TouchTarget, true, Erase);
            UIFactory.CreateButton(pad.transform, "0", UITheme.Blue, TouchTarget, true, () => AppendDigit(0));
            UIFactory.CreateButton(pad.transform, Strings.Ok, UITheme.Green, TouchTarget, true, Submit);
        }

        private void AppendDigit(int digit)
        {
            if (entry.Length >= ParentGateRules.MaxAnswerDigits) return;
            entry += digit.ToString();
            Refresh();
        }

        private void Erase()
        {
            if (entry.Length == 0) return;
            entry = entry.Substring(0, entry.Length - 1);
            Refresh();
        }

        private void Submit()
        {
            if (entry.Length == 0) return;
            int answer = int.Parse(entry);
            GateResult result = session.Submit(answer, Time.unscaledTimeAsDouble);
            entry = string.Empty;

            switch (result)
            {
                case GateResult.Passed:
                    Ui.CompleteParentGate();
                    return;
                case GateResult.Wrong:
                    feedbackLabel.text = Strings.TryAgain;
                    break;
                default:
                    feedbackLabel.text = Strings.PleaseWait;
                    break;
            }
            Refresh();
        }

        private void Refresh()
        {
            questionLabel.text = session.Question;
            entryLabel.text = entry.Length == 0 ? "_" : entry;
        }
    }
}
