using System.Collections;
using System.Collections.Generic;
using MinikDuello.Domain.Rounds;
using MinikDuello.Infra;
using UnityEngine;
using UnityEngine.UI;

namespace MinikDuello.UI
{
    /// <summary>Kart eşleştirme (yüzü açık) ve hafıza (yüzü kapalı) turları.</summary>
    public sealed class PairsRoundView : RoundView
    {
        private const float GridWidth = 960f;
        private const float Spacing = 20f;
        private const float MaxCell = 280f;
        private const float MismatchShowSeconds = 0.9f;
        private const float MismatchShowFaceUpSeconds = 0.45f;

        private sealed class CardUi
        {
            public Button Button;
            public Image Background;
            public GameObject Face;
            public GameObject Back;
        }

        private readonly List<CardUi> cards = new List<CardUi>();
        private PairsRound round;
        private PairsLogic logic;

        public override void Build(RoundSpec spec)
        {
            round = (PairsRound)spec;
            logic = new PairsLogic(round);

            int count = round.Cards.Count;
            int columns = count <= 4 ? 2 : (count <= 6 ? 3 : (count <= 8 ? 4 : (count <= 10 ? 5 : 4)));
            float cell = Mathf.Min(MaxCell, (GridWidth - (columns - 1) * Spacing) / columns);
            int rows = Mathf.CeilToInt(count / (float)columns);
            RectTransform grid = UIFactory.CreateGrid(transform, columns, new Vector2(cell, cell), new Vector2(Spacing, Spacing), rows);

            for (int i = 0; i < count; i++)
            {
                int index = i;
                Button button = MakeCard(grid, cell, UITheme.Card);
                button.transform.SetParent(grid, false);

                RectTransform face = VisualFactory.Create(button.transform, round.Cards[i].Visual, cell * 0.78f);
                face.anchorMin = face.anchorMax = new Vector2(0.5f, 0.5f);
                face.anchoredPosition = Vector2.zero;

                Image back = UIFactory.CreatePanel(button.transform, "Back", UITheme.Purple);
                UIFactory.Stretch(back.rectTransform);
                Text mark = UIFactory.CreateLabel(back.transform, "?", Mathf.RoundToInt(cell * 0.5f), UITheme.TextLight, 0f);
                UIFactory.Stretch(mark.rectTransform);
                Destroy(mark.GetComponent<LayoutElement>());

                button.onClick.AddListener(() => OnTap(index));
                cards.Add(new CardUi { Button = button, Background = button.GetComponent<Image>(), Face = face.gameObject, Back = back.gameObject });
                Refresh(i);
            }
        }

        public override void ShowHint()
        {
            int[] pair = logic.HintPair();
            if (pair == null) return;
            foreach (int i in pair) PulseOn(cards[i].Button, true);
        }

        private void OnTap(int index)
        {
            if (InputBlocked) return;
            PairTap result = logic.Tap(index);
            if (result == PairTap.Ignored) return;

            switch (result)
            {
                case PairTap.Selected:
                    Play(Sfx.Tap);
                    break;
                case PairTap.Match:
                    Play(Sfx.Correct);
                    ClearHints();
                    break;
                case PairTap.Completed:
                    Play(Sfx.Correct);
                    ClearHints();
                    RefreshAll();
                    Mistakes = logic.Mistakes;
                    Finish();
                    return;
                case PairTap.Mismatch:
                    Play(Sfx.Oops);
                    if (Effects.Instance != null)
                    {
                        Effects.Instance.Shake((RectTransform)cards[logic.PendingA].Button.transform);
                        Effects.Instance.Shake((RectTransform)cards[logic.PendingB].Button.transform);
                    }
                    StartCoroutine(ResolveMismatch());
                    break;
            }
            RefreshAll();
        }

        private IEnumerator ResolveMismatch()
        {
            yield return new WaitForSecondsRealtime(round.FaceDown ? MismatchShowSeconds : MismatchShowFaceUpSeconds);
            logic.ClearMismatch();
            RefreshAll();
            Mistakes = logic.Mistakes;
            if (Ctx.Hints && logic.Mistakes >= 2) ShowHint();
        }

        private void RefreshAll()
        {
            for (int i = 0; i < cards.Count; i++) Refresh(i);
        }

        private void Refresh(int i)
        {
            CardState state = logic.StateOf(i);
            CardUi ui = cards[i];
            bool faceVisible = !round.FaceDown || state != CardState.Hidden;
            ui.Face.SetActive(faceVisible);
            ui.Back.SetActive(!faceVisible);
            ui.Button.interactable = state == CardState.Hidden;
            ui.Background.color = state == CardState.Matched ? new Color32(200, 235, 200, 255)
                : state == CardState.Selected ? new Color32(255, 236, 170, 255) : UITheme.Card;
        }

        private void ClearHints()
        {
            foreach (CardUi c in cards) PulseOn(c.Button, false);
        }
    }
}
