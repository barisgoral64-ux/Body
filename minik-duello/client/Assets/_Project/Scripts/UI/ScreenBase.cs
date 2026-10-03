using MinikDuello.Core;
using UnityEngine;

namespace MinikDuello.UI
{
    /// <summary>Tüm ekranların tabanı. Ekranlar ilk gösterildiğinde üretilir (lazy), sonra yeniden kullanılır.</summary>
    public abstract class ScreenBase : MonoBehaviour
    {
        protected UIManager Ui { get; private set; }
        protected AppSettings Config { get; private set; }
        protected float TouchTarget { get; private set; }

        public ScreenId Id { get; private set; }

        public void Initialize(ScreenId id, UIManager ui, AppSettings config)
        {
            Id = id;
            Ui = ui;
            Config = config;
            TouchTarget = UIFactory.MinTouchTarget(config);
            Build();
            gameObject.SetActive(false);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            OnShown();
        }

        public void Hide() => gameObject.SetActive(false);

        protected abstract void Build();

        protected virtual void OnShown()
        {
        }
    }
}
