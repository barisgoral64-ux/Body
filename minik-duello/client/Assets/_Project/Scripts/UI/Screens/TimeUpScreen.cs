using MinikDuello.Core;

namespace MinikDuello.UI
{
    /// <summary>Ebeveynin günlük süre sınırı dolunca gösterilen nazik mola ekranı (korkutma yok).</summary>
    public sealed class TimeUpScreen : ScreenBase
    {
        protected override void Build()
        {
            UIFactory.CreateSpacer(transform, 200f);
            UIFactory.CreateLabel(transform, Strings.BreakTime, UITheme.TitleFontSize + 10, UITheme.TextDark);
            UIFactory.CreateLabel(transform, Strings.BreakBody, UITheme.BodyFontSize, UITheme.TextDark, UITheme.ContentWidth);
            UIFactory.CreateSpacer(transform, 120f);
            UIFactory.CreateButton(transform, Strings.Parent, UITheme.Neutral, TouchTarget, true, () => Ui.ShowBehindParentGate(ScreenId.ParentDashboard));
        }

        // Mola ekranından geri tuşuyla çıkılamaz.
        public override bool OnBackPressed() => true;
    }
}
