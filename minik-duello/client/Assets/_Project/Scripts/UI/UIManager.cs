using System;
using System.Collections;
using System.Collections.Generic;
using MinikDuello.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MinikDuello.UI
{
    public sealed class DialogButton
    {
        public string Label;
        public Color Color;
        public Action OnClick;

        public DialogButton(string label, Color color, Action onClick = null)
        {
            Label = label;
            Color = color;
            OnClick = onClick;
        }
    }

    /// <summary>Ekran yığını, ekran üretimi, açılır pencereler, yükleme göstergesi ve Android geri tuşu.</summary>
    public sealed class UIManager : MonoBehaviour
    {
        private const float ToastSeconds = 2.6f;
        private const float SpinnerSpeed = 220f;

        private readonly ScreenStack<ScreenId> stack = new ScreenStack<ScreenId>();
        private readonly Dictionary<ScreenId, ScreenBase> screens = new Dictionary<ScreenId, ScreenBase>();

        private AppSettings config;
        private CanvasLayers layers;
        private GameObject loadingOverlay;
        private RectTransform spinner;
        private GameObject dialog;
        private GameObject blocking;
        private Text blockingLabel;
        private Text toastLabel;
        private GameObject toastPanel;
        private Coroutine toastRoutine;
        private int loadingDepth;
        private ScreenId gateTarget = ScreenId.MainMenu;

        public NavContext Context { get; } = new NavContext();
        public ScreenId Current => stack.Current;
        public RectTransform OverlayRoot => layers.Overlay;
        public event Action<ScreenId> ScreenShown;

        public void Initialize(AppSettings settings)
        {
            config = settings ?? throw new ArgumentNullException(nameof(settings));
            UIFactory.EnsureEventSystem();
            layers = UIFactory.CreateCanvas(transform);
            Effects.Create(transform, layers.Overlay);
            BuildLoading();
            BuildBlocking();
            BuildToast();
            stack.Reset(ScreenId.MainMenu);
            Present();
        }

        // --- Gezinme ---

        public void Show(ScreenId id)
        {
            stack.Push(id);
            Present();
        }

        public void Replace(ScreenId id)
        {
            stack.ReplaceTop(id);
            Present();
        }

        /// <summary>Yığını sıfırlayıp ekranı zorla açar (örn. süre dolunca mola ekranı).</summary>
        public void ForceScreen(ScreenId id)
        {
            Context.ClearParent();
            CloseDialog();
            stack.Reset(id);
            Present();
        }

        public void GoHome()
        {
            Context.ClearParent();
            stack.Reset(ScreenId.MainMenu);
            Present();
        }

        public void Back()
        {
            if (loadingDepth > 0 || dialog != null || blocking.activeSelf) return;
            if (screens.TryGetValue(stack.Current, out ScreenBase current) && current.OnBackPressed()) return;
            if (stack.Pop(out _)) Present();
        }

        /// <summary>Hedef ekranı yetişkin doğrulamasından sonra açar.</summary>
        public void ShowBehindParentGate(ScreenId target)
        {
            gateTarget = target;
            Show(ScreenId.ParentGate);
        }

        public void CompleteParentGate()
        {
            stack.ReplaceTop(gateTarget);
            Present();
        }

        public bool IsShowing(ScreenId id) => stack.Current == id;

        // --- Katman: yükleme, bildirim, diyalog ---

        public void SetLoading(bool on)
        {
            loadingDepth = Mathf.Max(0, loadingDepth + (on ? 1 : -1));
            loadingOverlay.SetActive(loadingDepth > 0);
        }

        public void Toast(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            toastLabel.text = message;
            toastPanel.SetActive(true);
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(HideToastLater());
        }

        public void ShowDialog(string title, string message, params DialogButton[] buttons)
        {
            CloseDialog();
            dialog = new GameObject("Dialog", typeof(RectTransform));
            dialog.transform.SetParent(layers.Overlay, false);
            UIFactory.Stretch((RectTransform)dialog.transform);

            Image dim = UIFactory.CreateImage(dialog.transform, "Dim", UITheme.Dim);
            dim.raycastTarget = true;
            UIFactory.Stretch(dim.rectTransform);

            Image panel = UIFactory.CreatePanel(dialog.transform, "Panel", UITheme.Card, true);
            panel.rectTransform.sizeDelta = new Vector2(940f, 0f);
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 40, 40);
            layout.spacing = UITheme.Spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            float touch = UIFactory.MinTouchTarget(config);
            UIFactory.CreateLabel(panel.transform, title, UITheme.TitleFontSize - 14, UITheme.TextDark, 820f);
            if (!string.IsNullOrEmpty(message)) UIFactory.CreateLabel(panel.transform, message, UITheme.BodyFontSize, UITheme.TextDark, 820f);
            foreach (DialogButton b in buttons)
            {
                DialogButton captured = b;
                UIFactory.CreateButton(panel.transform, b.Label, b.Color, touch, false, () =>
                {
                    CloseDialog();
                    captured.OnClick?.Invoke();
                });
            }
        }

        public void CloseDialog()
        {
            if (dialog == null) return;
            Destroy(dialog);
            dialog = null;
        }

        public bool DialogOpen => dialog != null;

        /// <summary>Kapatılamayan tam ekran mesaj (örn. "Arkadaşına yeniden bağlanıyoruz…"). null → gizle.</summary>
        public void SetBlockingMessage(string message)
        {
            blocking.SetActive(!string.IsNullOrEmpty(message));
            blockingLabel.text = message ?? string.Empty;
        }

        // --- İç ---

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) Back();
            if (loadingOverlay != null && loadingOverlay.activeSelf) spinner.Rotate(0f, 0f, -SpinnerSpeed * Time.unscaledDeltaTime);
        }

        private void Present()
        {
            foreach (ScreenBase screen in screens.Values)
            {
                if (screen.gameObject.activeSelf) screen.Hide();
            }
            GetOrCreate(stack.Current).Show();
            ScreenShown?.Invoke(stack.Current);
        }

        private ScreenBase GetOrCreate(ScreenId id)
        {
            if (screens.TryGetValue(id, out ScreenBase existing)) return existing;

            RectTransform root = UIFactory.CreateScreenRoot(layers.Safe, id.ToString());
            ScreenBase screen = AddScreenComponent(id, root.gameObject);
            screens[id] = screen;
            screen.Initialize(id, this, config);
            Log.Debug("UI", "Ekran oluşturuldu: " + id);
            return screen;
        }

        private static ScreenBase AddScreenComponent(ScreenId id, GameObject target)
        {
            switch (id)
            {
                case ScreenId.MainMenu: return target.AddComponent<MainMenuScreen>();
                case ScreenId.PlayMenu: return target.AddComponent<PlayMenuScreen>();
                case ScreenId.WorldSelect: return target.AddComponent<WorldSelectScreen>();
                case ScreenId.LevelSelect: return target.AddComponent<LevelSelectScreen>();
                case ScreenId.Game: return target.AddComponent<GameScreen>();
                case ScreenId.Result: return target.AddComponent<ResultScreen>();
                case ScreenId.Friends: return target.AddComponent<FriendsScreen>();
                case ScreenId.AddFriend: return target.AddComponent<AddFriendScreen>();
                case ScreenId.Requests: return target.AddComponent<RequestsScreen>();
                case ScreenId.Weekly: return target.AddComponent<WeeklyScreen>();
                case ScreenId.Character: return target.AddComponent<CharacterScreen>();
                case ScreenId.Rewards: return target.AddComponent<RewardsScreen>();
                case ScreenId.Profile: return target.AddComponent<ProfileScreen>();
                case ScreenId.DailyReward: return target.AddComponent<DailyRewardScreen>();
                case ScreenId.ParentGate: return target.AddComponent<ParentGateScreen>();
                case ScreenId.ParentDashboard: return target.AddComponent<ParentDashboardScreen>();
                case ScreenId.FriendPlay: return target.AddComponent<FriendPlayScreen>();
                case ScreenId.Lobby: return target.AddComponent<LobbyScreen>();
                case ScreenId.MatchGame: return target.AddComponent<MatchGameScreen>();
                case ScreenId.MatchResult: return target.AddComponent<MatchResultScreen>();
                case ScreenId.TimeUp: return target.AddComponent<TimeUpScreen>();
                default: throw new ArgumentOutOfRangeException(nameof(id), id, "Ekran tanımlı değil");
            }
        }

        private void BuildLoading()
        {
            loadingOverlay = new GameObject("Loading", typeof(RectTransform));
            loadingOverlay.transform.SetParent(layers.Overlay, false);
            UIFactory.Stretch((RectTransform)loadingOverlay.transform);
            Image dim = UIFactory.CreateImage(loadingOverlay.transform, "Dim", new Color(0f, 0f, 0f, 0.35f));
            dim.raycastTarget = true; // yükleme sırasında dokunmaları engeller
            UIFactory.Stretch(dim.rectTransform);
            Image star = UIFactory.CreateImage(loadingOverlay.transform, "Spinner", UITheme.Yellow);
            star.sprite = SpriteFactory.Shape("star");
            star.rectTransform.sizeDelta = new Vector2(180f, 180f);
            spinner = star.rectTransform;
            loadingOverlay.SetActive(false);
        }

        private void BuildBlocking()
        {
            blocking = new GameObject("Blocking", typeof(RectTransform));
            blocking.transform.SetParent(layers.Overlay, false);
            UIFactory.Stretch((RectTransform)blocking.transform);
            Image dim = UIFactory.CreateImage(blocking.transform, "Dim", new Color(0f, 0f, 0f, 0.6f));
            dim.raycastTarget = true;
            UIFactory.Stretch(dim.rectTransform);
            blockingLabel = UIFactory.CreateLabel(blocking.transform, string.Empty, UITheme.TitleFontSize - 14, UITheme.TextLight, 900f);
            blockingLabel.rectTransform.sizeDelta = new Vector2(900f, 400f);
            blocking.SetActive(false);
        }

        private void BuildToast()
        {
            Image panel = UIFactory.CreatePanel(layers.Overlay, "Toast", new Color(0.15f, 0.12f, 0.1f, 0.92f));
            panel.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            panel.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            panel.rectTransform.pivot = new Vector2(0.5f, 0f);
            panel.rectTransform.anchoredPosition = new Vector2(0f, 160f);
            panel.rectTransform.sizeDelta = new Vector2(900f, 130f);
            toastLabel = UIFactory.CreateLabel(panel.transform, string.Empty, UITheme.BodyFontSize - 4, UITheme.TextLight, 860f);
            UIFactory.Stretch(toastLabel.rectTransform);
            Destroy(toastLabel.GetComponent<LayoutElement>());
            toastPanel = panel.gameObject;
            toastPanel.SetActive(false);
        }

        private IEnumerator HideToastLater()
        {
            yield return new WaitForSecondsRealtime(ToastSeconds);
            toastPanel.SetActive(false);
            toastRoutine = null;
        }
    }
}
