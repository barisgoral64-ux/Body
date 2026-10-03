using System.Collections;
using MinikDuello.Core;
using MinikDuello.Domain.Levels;
using MinikDuello.Domain.Rounds;
using MinikDuello.Infra;
using MinikDuello.Services;
using MinikDuello.Services.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace MinikDuello.UI
{
    /// <summary>Bir bölümün oynanışı: turları sırayla gösterir, puanlar ve sonucu kaydeder. Başarısızlık yoktur.</summary>
    public sealed class GameScreen : ScreenBase
    {
        private const float NextRoundDelay = 0.85f;
        private const int ForcedMistakesOnTimeout = 3;

        private LevelSession session;
        private RoundView view;
        private Text prompt;
        private RectTransform dots;
        private RectTransform container;
        private RectTransform dragLayer;
        private Image timeBar;
        private LevelManager levels;
        private AudioManager audio;
        private Coroutine advance;
        private bool hints;
        private float unlockAt;
        private float roundStart;
        private float roundTimeLimit;
        private bool timedOut;
        private bool roundDone;

        protected override void Build()
        {
            RectTransform top = UIFactory.CreateRow(transform, UITheme.Spacing, TouchTarget);
            UIFactory.CreateButton(top, Strings.Back, UITheme.Neutral, TouchTarget, true, Ui.Back);
            dots = UIFactory.CreateRow(top, 12f, 60f);
            dots.GetComponent<LayoutElement>().preferredWidth = 520f;
            UIFactory.CreateButton(top, "DİNLE", UITheme.Blue, TouchTarget, true, SpeakPrompt);

            prompt = UIFactory.CreateLabel(transform, string.Empty, UITheme.TitleFontSize - 10, UITheme.TextDark, UITheme.ContentWidth);
            prompt.GetComponent<LayoutElement>().preferredHeight = 130f;

            Image barBg = UIFactory.CreatePanel(transform, "TimeBg", new Color(0f, 0f, 0f, 0.08f));
            var barLe = barBg.gameObject.AddComponent<LayoutElement>();
            barLe.preferredWidth = UITheme.ContentWidth;
            barLe.preferredHeight = 18f;
            timeBar = UIFactory.CreatePanel(barBg.transform, "TimeBar", UITheme.Green);
            timeBar.rectTransform.anchorMin = Vector2.zero;
            timeBar.rectTransform.anchorMax = Vector2.one;
            timeBar.rectTransform.offsetMin = Vector2.zero;
            timeBar.rectTransform.offsetMax = Vector2.zero;

            var containerGo = new GameObject("RoundContainer", typeof(RectTransform), typeof(LayoutElement), typeof(VerticalLayoutGroup));
            containerGo.transform.SetParent(transform, false);
            container = (RectTransform)containerGo.transform;
            var le = containerGo.GetComponent<LayoutElement>();
            le.preferredWidth = UITheme.ContentWidth;
            le.flexibleHeight = 1f;
            var layout = containerGo.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var dragGo = new GameObject("DragLayer", typeof(RectTransform), typeof(LayoutElement));
            dragGo.transform.SetParent(transform, false);
            dragGo.GetComponent<LayoutElement>().ignoreLayout = true;
            dragLayer = (RectTransform)dragGo.transform;
            UIFactory.Stretch(dragLayer);
        }

        protected override void OnShown()
        {
            levels = ServiceLocator.Get<LevelManager>();
            audio = ServiceLocator.Get<AudioManager>();
            DifficultyManager.Plan plan = levels.PlanFor(Nav.SelectedLevel);
            hints = plan.HintsEnabled;

            var generator = new RoundGenerator(new System.Random());
            session = new LevelSession(plan.Level, generator.Generate(plan.Level), Time.unscaledTimeAsDouble);
            roundTimeLimit = plan.Level.TimeLimitSeconds > 0 ? plan.Level.TimeLimitSeconds / (float)plan.Level.RoundCount : 0f;
            BuildDots();
            StartRound();
        }

        protected override void OnHidden()
        {
            if (advance != null) StopCoroutine(advance);
            advance = null;
            ClearView();
        }

        private void Update()
        {
            if (session == null || roundDone || session.IsFinished || roundTimeLimit <= 0f)
            {
                if (timeBar != null) timeBar.transform.parent.gameObject.SetActive(roundTimeLimit > 0f && !roundDone);
                return;
            }
            float elapsed = Time.unscaledTime - roundStart;
            float remaining = Mathf.Clamp01(1f - elapsed / roundTimeLimit);
            timeBar.rectTransform.anchorMax = new Vector2(remaining, 1f);
            // Süre dolunca ceza yok: yalnızca ipucu açılır ve tur asgari puanla kapanır.
            if (!timedOut && elapsed > roundTimeLimit)
            {
                timedOut = true;
                view.ShowHint();
            }
        }

        private void StartRound()
        {
            ClearView();
            roundDone = false;
            timedOut = false;
            RoundSpec spec = session.Current;
            prompt.text = Prompts.Text(spec.PromptKey, spec.PromptParams);

            var context = new RoundContext
            {
                TouchTarget = TouchTarget,
                Hints = hints,
                Locked = () => Time.unscaledTime < unlockAt,
                Completed = OnRoundCompleted,
                DragLayer = dragLayer,
                Audio = audio
            };
            view = RoundViewFactory.Create(spec, container, context);
            roundStart = Time.unscaledTime;
            unlockAt = roundStart + Config.RoundIntroLockMs / 1000f;
            timeBar.rectTransform.anchorMax = Vector2.one;
            SpeakPrompt();
        }

        private void SpeakPrompt()
        {
            if (session == null || session.IsFinished) return;
            ServiceLocator.Get<IVoiceService>().Speak(Prompts.VoiceKey(session.Current.PromptKey, session.Current.PromptParams));
        }

        private void OnRoundCompleted(int mistakes)
        {
            roundDone = true;
            int effective = timedOut ? Mathf.Max(mistakes, ForcedMistakesOnTimeout) : mistakes;
            session.CompleteRound(effective);
            BuildDots();
            if (Effects.Instance != null) Effects.Instance.BurstAtCenter(true);
            audio.Play(Sfx.Star);
            advance = StartCoroutine(AdvanceAfterDelay());
        }

        private IEnumerator AdvanceAfterDelay()
        {
            yield return new WaitForSecondsRealtime(NextRoundDelay);
            advance = null;
            if (session.IsFinished) FinishLevel();
            else StartRound();
        }

        private void FinishLevel()
        {
            audio.Play(Sfx.Win);
            int durationMs = session.ElapsedMs(Time.unscaledTimeAsDouble);
            Result<LevelCompletion> local = levels.CompleteLocal(session.Level.LevelId, session.Score, durationMs);
            if (!local.IsOk)
            {
                Ui.Toast(Messages.For(local.Error));
                Ui.Back();
                return;
            }

            LevelCompletion completion = local.Value;
            Nav.LastCompletion = completion;
            Nav.LastScore = session.Score;
            Nav.LastMistakes = session.TotalMistakes;
            int levelId = session.Level.LevelId;
            Ui.Replace(ScreenId.Result);

            // Sunucuya gönderim arka planda: sonuç ekranı beklemez, ödüller gelince güncellenir.
            RunAsync(async () =>
            {
                completion.Server = await levels.FlushPendingAsync(levelId);
                EventBus.Publish(new LevelServerResult { Completion = completion });
                await ServiceLocator.Get<PlayerManager>().RefreshAsync();
                await ServiceLocator.Get<RewardManager>().RefreshInventoryAsync();
            });
        }

        private void BuildDots()
        {
            UIFactory.ClearChildren(dots);
            for (int i = 0; i < session.RoundCount; i++)
            {
                Image dot = UIFactory.CreateImage(dots, "Dot", i < session.Index ? UITheme.Yellow : new Color32(210, 204, 190, 255));
                dot.sprite = SpriteFactory.Shape("circle");
                var le = dot.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 44f;
                le.preferredHeight = 44f;
            }
        }

        private void ClearView()
        {
            if (view != null) Destroy(view.gameObject);
            view = null;
            UIFactory.ClearChildren(dragLayer);
        }
    }

    public sealed class LevelServerResult
    {
        public LevelCompletion Completion;
    }
}
