using System;
using System.Collections.Generic;
using MinikDuello.Core;
using UnityEngine;

namespace MinikDuello.UI
{
    /// <summary>Ekran yığını, ekran üretimi ve Android geri tuşu. Yetişkin kapısı arkasındaki ekranları yönetir.</summary>
    public sealed class UIManager : MonoBehaviour
    {
        private readonly ScreenStack<ScreenId> stack = new ScreenStack<ScreenId>();
        private readonly Dictionary<ScreenId, ScreenBase> screens = new Dictionary<ScreenId, ScreenBase>();

        private GameConfig config;
        private RectTransform safeRoot;
        private ScreenId gateTarget = ScreenId.MainMenu;

        public ScreenId Current => stack.Current;

        public void Initialize(GameConfig gameConfig)
        {
            config = gameConfig != null ? gameConfig : throw new ArgumentNullException(nameof(gameConfig));
            UIFactory.EnsureEventSystem();
            safeRoot = UIFactory.CreateCanvasWithSafeArea(transform);
            stack.Reset(ScreenId.MainMenu);
            Present();
        }

        public void Show(ScreenId id)
        {
            stack.Push(id);
            Present();
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

        public void Back()
        {
            if (stack.Pop(out _)) Present();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) Back();
        }

        private void Present()
        {
            foreach (ScreenBase screen in screens.Values) screen.Hide();
            GetOrCreate(stack.Current).Show();
        }

        private ScreenBase GetOrCreate(ScreenId id)
        {
            if (screens.TryGetValue(id, out ScreenBase existing)) return existing;

            RectTransform root = UIFactory.CreateScreenRoot(safeRoot, id.ToString());
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
                case ScreenId.ParentGate: return target.AddComponent<ParentGateScreen>();
                default: return target.AddComponent<PlaceholderScreen>();
            }
        }
    }
}
