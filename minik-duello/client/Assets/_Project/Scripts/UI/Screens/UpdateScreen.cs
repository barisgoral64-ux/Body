using MinikDuello.Core;
using MinikDuello.Services.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace MinikDuello.UI
{
    /// <summary>
    /// Zorunlu güncelleme ekranı (bu sürüm artık desteklenmiyor). Çocuk dışarı çıkamaz; mağazaya gidiş yetişkin kapısının arkasındadır.
    /// </summary>
    public sealed class UpdateScreen : ScreenBase
    {
        private Text versionLabel;

        protected override void Build()
        {
            UIFactory.CreateSpacer(transform, 160f);
            UIFactory.CreateLabel(transform, Strings.UpdateTitle, UITheme.TitleFontSize, UITheme.TextDark, UITheme.ContentWidth);
            UIFactory.CreateLabel(transform, Strings.UpdateRequiredBody, UITheme.BodyFontSize, UITheme.TextDark, UITheme.ContentWidth);
            UIFactory.CreateSpacer(transform, 60f);
            UIFactory.CreateButton(transform, Strings.UpdateButton, UITheme.Green, TouchTarget, false, OpenStoreBehindGate);
            versionLabel = UIFactory.CreateLabel(transform, string.Empty, UITheme.SmallFontSize + 2, UITheme.Neutral, UITheme.ContentWidth);
        }

        protected override void OnShown()
        {
            // Sürüm bilgisi (hangi sürümde olduğunu ebeveyn görsün).
            versionLabel.text = Strings.Version + " " + ServiceLocator.Get<VersionChecker>().CurrentVersion;
        }

        // Güncelleme ekranından geri tuşuyla çıkılamaz.
        public override bool OnBackPressed() => true;

        private void OpenStoreBehindGate()
        {
            string url = ServiceLocator.Get<VersionChecker>().Last?.StoreUrl;
            Ui.RunBehindParentGate(() => StoreLink.Open(url, Ui));
        }
    }

    public static class StoreLink
    {
        public static void Open(string url, UIManager ui)
        {
            if (string.IsNullOrEmpty(url))
            {
                ui.Toast("Mağaza adresi bulunamadı. Uygulamayı mağazadan güncelleyin.");
                return;
            }
            Application.OpenURL(url);
        }
    }
}
