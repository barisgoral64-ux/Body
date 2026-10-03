using System;
using System.Threading.Tasks;
using UnityEngine.UI;
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
        protected NavContext Nav => Ui.Context;

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

        public void Hide()
        {
            OnHidden();
            gameObject.SetActive(false);
        }

        /// <summary>Geri tuşu: true dönerse ekran kendisi işledi, yığın değişmez.</summary>
        public virtual bool OnBackPressed() => false;

        protected abstract void Build();

        protected virtual void OnShown()
        {
        }

        protected virtual void OnHidden()
        {
        }

        /// <summary>Üst başlık: sol üstte küçük geri düğmesi ve başlık.</summary>
        protected void BuildHeader(string title, bool showBack = true)
        {
            RectTransform row = UIFactory.CreateRow(transform, UITheme.Spacing, TouchTarget);
            if (showBack) UIFactory.CreateButton(row, Strings.Back, UITheme.Neutral, TouchTarget, true, Ui.Back);
            Text titleLabel = UIFactory.CreateLabel(row, title, UITheme.ButtonFontSize + 4, UITheme.TextDark, 520f);
            titleLabel.alignment = TextAnchor.MiddleCenter;
        }

        /// <summary>
        /// Eşzamansız iş: hata oyunu çökertmez, ekran bu arada kapandıysa sonuç yok sayılır.
        /// </summary>
        protected async void RunAsync(Func<Task> work, bool showLoading = false)
        {
            if (showLoading) Ui.SetLoading(true);
            try
            {
                await work();
            }
            catch (Exception exception)
            {
                Log.Error("UI", exception);
                Ui.Toast(Strings.TryAgain);
            }
            finally
            {
                if (showLoading && Ui != null) Ui.SetLoading(false);
            }
        }

        protected bool IsAlive => this != null && gameObject != null && gameObject.activeInHierarchy;
    }
}
