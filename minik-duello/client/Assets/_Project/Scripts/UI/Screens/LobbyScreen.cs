using System.Collections;
using MinikDuello.Core;
using MinikDuello.Domain.Net;
using MinikDuello.Infra;
using MinikDuello.Services.Managers;
using MinikDuello.Services.Realtime;
using UnityEngine;
using UnityEngine.UI;

namespace MinikDuello.UI
{
    /// <summary>Oyun odası: iki oyuncu, HAZIR düğmesi, 3-2-1-BAŞLA! geri sayımı ve hazır mesajlar.</summary>
    public sealed class LobbyScreen : ScreenBase
    {
        private MatchSession match;
        private RectTransform players;
        private Text status;
        private Text countdown;
        private Button ready;
        private Coroutine countdownRoutine;
        private bool readySent;

        protected override void Build()
        {
            BuildHeader("OYUN ODASI", false);
            players = UIFactory.CreateRow(transform, 30f, 360f);
            status = UIFactory.CreateLabel(transform, string.Empty, UITheme.BodyFontSize, UITheme.TextDark, UITheme.ContentWidth);
            countdown = UIFactory.CreateLabel(transform, string.Empty, 200, UITheme.Primary, UITheme.ContentWidth);
            countdown.GetComponent<LayoutElement>().preferredHeight = 240f;
            ready = UIFactory.CreateButton(transform, Strings.Ready, UITheme.Green, TouchTarget, false, OnReady);
            UIFactory.CreateButton(transform, "AYRIL", UITheme.Neutral, TouchTarget, true, Leave);
            QuickChatBar.Create(transform, TouchTarget, ServiceLocator.Get<MatchSession>());
        }

        protected override void OnShown()
        {
            match = ServiceLocator.Get<MatchSession>();
            match.CountdownStarted += OnCountdown;
            readySent = false;
            ready.gameObject.SetActive(true);
            countdown.text = string.Empty;
            status.text = match.Phase == MatchPhase.Countdown ? Strings.Ready : "Hazır olunca düğmeye bas!";
            RenderPlayers();
        }

        protected override void OnHidden()
        {
            if (match != null) match.CountdownStarted -= OnCountdown;
            if (countdownRoutine != null) StopCoroutine(countdownRoutine);
            countdownRoutine = null;
        }

        public override bool OnBackPressed()
        {
            Leave();
            return true;
        }

        private void RenderPlayers()
        {
            UIFactory.ClearChildren(players);
            var player = ServiceLocator.Get<PlayerManager>();
            var characters = ServiceLocator.Get<CharacterManager>();
            Card(player.Username, player.Character, characters.EquippedBySlot());
            Card(Nav.OpponentName ?? "Arkadaşın", Nav.OpponentCharacter, null);
        }

        private void Card(string name, string character, System.Collections.Generic.IDictionary<string, string> equipped)
        {
            RectTransform col = UIFactory.CreateColumn(players, 8f, 430f);
            UIKit.Avatar(col, character, equipped, 240f);
            UIFactory.CreateLabel(col, name, UITheme.SmallFontSize + 4, UITheme.TextDark, 430f);
        }

        private void OnReady()
        {
            if (readySent) return;
            if (!match.Ready())
            {
                Ui.Toast(Strings.OfflineNote);
                return;
            }
            readySent = true;
            ready.gameObject.SetActive(false);
            status.text = "Arkadaşını bekliyoruz…";
        }

        private void OnCountdown(int seconds)
        {
            ready.gameObject.SetActive(false);
            status.text = string.Empty;
            if (countdownRoutine != null) StopCoroutine(countdownRoutine);
            countdownRoutine = StartCoroutine(CountdownRoutine(seconds));
        }

        private IEnumerator CountdownRoutine(int seconds)
        {
            var audio = ServiceLocator.Get<AudioManager>();
            for (int i = seconds; i >= 1; i--)
            {
                countdown.text = i.ToString();
                audio.Play(Sfx.Tick);
                if (Effects.Instance != null) Effects.Instance.Pop(countdown.rectTransform);
                yield return new WaitForSecondsRealtime(1f);
            }
            countdown.text = Strings.Go;
            audio.Play(Sfx.Go);
        }

        private void Leave()
        {
            match.Leave();
            Ui.GoHome();
        }
    }
}
