using System.Collections;
using System.Collections.Generic;
using MinikDuello.Core;
using MinikDuello.Domain.Net;
using MinikDuello.Domain.Rounds;
using MinikDuello.Infra;
using MinikDuello.Services;
using MinikDuello.Services.Api;
using MinikDuello.Services.Realtime;
using UnityEngine;
using UnityEngine.UI;

namespace MinikDuello.UI
{
    /// <summary>
    /// Çok oyunculu tur ekranı. İstemci yalnızca dokunulan seçimi gönderir; doğruluk, süre, puan ve sıralama sunucudadır.
    /// Geri bildirim yumuşaktır: yanlış cevapta "Çok yaklaştın!", asla olumsuz ifade yok.
    /// </summary>
    public sealed class MatchGameScreen : ScreenBase
    {
        private static readonly Color CorrectColor = new Color32(200, 235, 200, 255);
        private static readonly Color WrongColor = new Color32(255, 224, 178, 255);
        private static readonly Color PickedColor = new Color32(255, 236, 170, 255);

        private MatchSession match;
        private AuthSession auth;
        private Text myScore;
        private Text opponentScore;
        private Text teamLabel;
        private Text prompt;
        private Text banner;
        private RectTransform container;
        private readonly Dictionary<string, Button> cards = new Dictionary<string, Button>();
        private readonly Queue<string> pendingTaps = new Queue<string>();
        private readonly HashSet<string> tapped = new HashSet<string>();
        private RoundDto round;
        private float answerUnlockAt;
        private Coroutine memoryRoutine;
        private RectTransform memoryRow;

        protected override void Build()
        {
            RectTransform top = UIFactory.CreateRow(transform, 16f, 90f);
            UIFactory.CreateButton(top, "AYRIL", UITheme.Neutral, 90f, true, ConfirmLeave);
            myScore = UIKit.Chip(top, string.Empty, new Color32(200, 235, 200, 255), 330f, UITheme.SmallFontSize + 2);
            opponentScore = UIKit.Chip(top, string.Empty, new Color32(255, 224, 178, 255), 330f, UITheme.SmallFontSize + 2);
            teamLabel = UIFactory.CreateLabel(transform, string.Empty, UITheme.SmallFontSize + 4, UITheme.Neutral, UITheme.ContentWidth);
            banner = UIFactory.CreateLabel(transform, string.Empty, UITheme.SmallFontSize + 4, UITheme.Primary, UITheme.ContentWidth);
            prompt = UIFactory.CreateLabel(transform, string.Empty, UITheme.TitleFontSize - 14, UITheme.TextDark, UITheme.ContentWidth);
            prompt.GetComponent<LayoutElement>().preferredHeight = 120f;

            var containerGo = new GameObject("MatchRound", typeof(RectTransform), typeof(LayoutElement), typeof(VerticalLayoutGroup));
            containerGo.transform.SetParent(transform, false);
            container = (RectTransform)containerGo.transform;
            containerGo.GetComponent<LayoutElement>().preferredWidth = UITheme.ContentWidth;
            containerGo.GetComponent<LayoutElement>().flexibleHeight = 1f;
            var layout = containerGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 24f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            QuickChatBar.Create(transform, TouchTarget, ServiceLocator.Get<MatchSession>());
        }

        protected override void OnShown()
        {
            match = ServiceLocator.Get<MatchSession>();
            auth = ServiceLocator.Get<AuthSession>();
            match.RoundStarted += OnRound;
            match.AnswerAcknowledged += OnAck;
            match.RoundEnded += OnRoundEnded;
            match.OpponentDisconnected += OnOpponentDisconnected;
            match.OpponentReconnected += OnOpponentReconnected;
            banner.text = string.Empty;
            teamLabel.text = match.IsCoop ? Strings.PlayTogether : string.Empty;
            UpdateScores(match.Scores);
            if (match.CurrentRound != null && match.Phase == MatchPhase.Playing) OnRound(match.CurrentRound);
            else Clear();
        }

        protected override void OnHidden()
        {
            if (match == null) return;
            match.RoundStarted -= OnRound;
            match.AnswerAcknowledged -= OnAck;
            match.RoundEnded -= OnRoundEnded;
            match.OpponentDisconnected -= OnOpponentDisconnected;
            match.OpponentReconnected -= OnOpponentReconnected;
            Clear();
        }

        public override bool OnBackPressed()
        {
            ConfirmLeave();
            return true;
        }

        private void ConfirmLeave()
        {
            Ui.ShowDialog("Oyundan ayrılalım mı?", "Arkadaşın seni bekler.",
                new DialogButton("AYRIL", UITheme.Pink, () => { match.Leave(); Ui.GoHome(); }),
                new DialogButton("DEVAM", UITheme.Green));
        }

        // --- Sunucu olayları ---

        private void OnRound(RoundDto r)
        {
            Clear();
            round = r;
            prompt.text = PromptFor(r);
            answerUnlockAt = Time.unscaledTime + r.ShowMs / 1000f;
            BuildRound(r);
            ServiceLocator.Get<IVoiceService>().Speak(VoiceKeyFor(r));
        }

        private void OnAck(AnswerAckDto ack)
        {
            string id = pendingTaps.Count > 0 ? pendingTaps.Dequeue() : null;
            UpdateMine(ack.Total);
            if (id == null || !cards.TryGetValue(id, out Button card)) return;

            var audio = ServiceLocator.Get<AudioManager>();
            card.GetComponent<Image>().color = ack.Correct ? CorrectColor : WrongColor;
            if (Effects.Instance != null) Effects.Instance.Pop((RectTransform)card.transform);
            if (ack.Correct)
            {
                audio.Play(Sfx.Correct);
                banner.text = "+" + ack.Points;
            }
            else
            {
                audio.Play(Sfx.Oops);
                banner.text = ack.Taken ? "Arkadaşın önce aldı!" : Strings.AlmostThere;
            }
        }

        private void OnRoundEnded(RoundResultDto result)
        {
            UpdateScores(result.Scores);
            foreach (string id in result.CorrectChoiceIds)
            {
                if (cards.TryGetValue(id, out Button card)) card.GetComponent<Image>().color = CorrectColor;
            }
            foreach (Button b in cards.Values) b.interactable = false;
            if (result.Team != null) teamLabel.text = "Takım: " + result.Team.Progress + " / " + result.Team.Target;
            if (memoryRoutine != null) StopCoroutine(memoryRoutine);
        }

        private void OnOpponentDisconnected() => banner.text = "Arkadaşın yeniden bağlanıyor…";
        private void OnOpponentReconnected() => banner.text = string.Empty;

        // --- Çizim ---

        private void BuildRound(RoundDto r)
        {
            switch (r.Kind)
            {
                case "number": BuildBalloons(r); break;
                case "memory": BuildMemory(r); break;
                case "puzzle": BuildPuzzle(r); break;
            }

            bool stars = r.Kind == "stars";
            int n = r.Choices.Count;
            int columns = stars ? 3 : 2;
            float cell = stars ? 280f : 420f;
            int rows = Mathf.CeilToInt(n / (float)columns);
            RectTransform grid = UIFactory.CreateGrid(container, columns, new Vector2(cell, cell * (stars ? 1f : 0.8f)), new Vector2(24f, 24f), rows);
            foreach (RoundChoiceDto choice in r.Choices)
            {
                RoundChoiceDto captured = choice;
                var go = new GameObject("Choice_" + choice.Id, typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(grid, false);
                var bg = go.GetComponent<Image>();
                bg.sprite = SpriteFactory.RoundedRect();
                bg.type = Image.Type.Sliced;
                bg.color = UITheme.Card;
                var button = go.GetComponent<Button>();
                button.targetGraphic = bg;
                button.onClick.AddListener(() => OnTap(captured.Id, button));
                RectTransform visual = VisualFactory.Create(go.transform, VisualFor(r.Kind, choice.Glyph), cell * 0.6f);
                visual.anchorMin = visual.anchorMax = new Vector2(0.5f, 0.5f);
                visual.anchoredPosition = Vector2.zero;
                cards[choice.Id] = button;
                // Hafıza turunda gösterim bitene kadar seçenekler kapalı.
                button.interactable = r.ShowMs <= 0;
            }
            if (r.ShowMs > 0) memoryRoutine = StartCoroutine(UnlockAfter(r.ShowMs / 1000f));
        }

        private void BuildBalloons(RoundDto r)
        {
            int count = int.Parse(r.Param("count"));
            int rows = Mathf.CeilToInt(count / 5f);
            RectTransform grid = UIFactory.CreateGrid(container, 5, new Vector2(120f, 120f), new Vector2(20f, 20f), rows);
            for (int i = 0; i < count; i++) VisualFactory.Create(grid, new VisualRef(VisualKind.Symbol, "balloon"), 120f);
        }

        private void BuildMemory(RoundDto r)
        {
            string[] sequence = r.Param("sequence").Split(',');
            memoryRow = UIFactory.CreateRow(container, 16f, 190f);
            foreach (string key in sequence)
            {
                RectTransform v = VisualFactory.Create(memoryRow, new VisualRef(VisualKind.Symbol, key), 180f);
                var le = v.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 180f;
                le.preferredHeight = 180f;
            }
        }

        private void BuildPuzzle(RoundDto r)
        {
            int pieces = int.Parse(r.Param("pieces"));
            int missing = int.Parse(r.Param("missing")) - 1;
            string image = r.Param("image");
            RectTransform row = UIFactory.CreateRow(container, 12f, 170f);
            for (int i = 0; i < pieces; i++)
            {
                VisualRef v = i == missing ? new VisualRef(VisualKind.Unknown, "?") : new VisualRef(VisualKind.Piece, image + "_" + i);
                RectTransform vis = VisualFactory.Create(row, v, 160f);
                var le = vis.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 160f;
                le.preferredHeight = 160f;
            }
        }

        private IEnumerator UnlockAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (memoryRow != null) Destroy(memoryRow.gameObject); // ezber bitti: dizi gizlenir
            foreach (Button b in cards.Values) b.interactable = true;
            prompt.text = "Hangisiydi? " + round.Param("position") + ". sıradaki";
        }

        private void OnTap(string id, Button button)
        {
            if (Time.unscaledTime < answerUnlockAt || round == null) return;
            if (round.Multi && tapped.Contains(id)) return;
            if (!match.Answer(id)) return;

            tapped.Add(id);
            pendingTaps.Enqueue(id);
            ServiceLocator.Get<AudioManager>().Play(Sfx.Tap);
            button.GetComponent<Image>().color = PickedColor;
            if (!round.Multi)
            {
                foreach (Button b in cards.Values) b.interactable = false;
            }
        }

        // --- Skorlar ---

        private void UpdateScores(Dictionary<string, int> scores)
        {
            if (scores == null) return;
            scores.TryGetValue(auth.PlayerId ?? string.Empty, out int mine);
            int theirs = 0;
            foreach (KeyValuePair<string, int> kv in scores)
            {
                if (kv.Key != auth.PlayerId) theirs = kv.Value;
            }
            UpdateMine(mine);
            opponentScore.text = (Nav.OpponentName ?? "Arkadaş") + ": " + theirs;
        }

        private void UpdateMine(int total) => myScore.text = "Sen: " + total;

        private void Clear()
        {
            if (memoryRoutine != null) StopCoroutine(memoryRoutine);
            memoryRoutine = null;
            UIFactory.ClearChildren(container);
            cards.Clear();
            pendingTaps.Clear();
            tapped.Clear();
            memoryRow = null;
        }

        // --- Eşlemeler ---

        private static string PromptFor(RoundDto r)
        {
            var p = new Dictionary<string, string> { { "target", r.Param("target") } };
            switch (r.Kind)
            {
                case "color": return Prompts.Text("findColor", p);
                case "shape": return Prompts.Text("findShape", p);
                case "number": return Prompts.Text("countSelect", p);
                case "memory": return "Ezberle!";
                case "puzzle": return Prompts.Text("puzzleMissing", p);
                case "stars": return Prompts.Text("findStars", p);
                default: return string.Empty;
            }
        }

        private static string VoiceKeyFor(RoundDto r) =>
            Prompts.VoiceKey(r.PromptKey, new Dictionary<string, string> { { "target", r.Param("target") } });

        private static VisualRef VisualFor(string kind, string glyph)
        {
            switch (kind)
            {
                case "color": return new VisualRef(VisualKind.Color, glyph);
                case "number": return new VisualRef(VisualKind.Number, glyph);
                case "memory": return new VisualRef(VisualKind.Symbol, glyph);
                case "puzzle": return new VisualRef(VisualKind.Piece, glyph);
                default: return new VisualRef(VisualKind.Shape, glyph);
            }
        }
    }
}
