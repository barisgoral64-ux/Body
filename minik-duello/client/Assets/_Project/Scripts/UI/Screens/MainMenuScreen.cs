using MinikDuello.Core;

namespace MinikDuello.UI
{
    public sealed class MainMenuScreen : ScreenBase
    {
        protected override void Build()
        {
            UIFactory.CreateLabel(transform, Strings.GameTitle, UITheme.TitleFontSize, UITheme.TextDark);
            UIFactory.CreateButton(transform, Strings.Play, UITheme.Primary, TouchTarget, false, () => Ui.Show(ScreenId.PlayMenu));
            UIFactory.CreateButton(transform, Strings.Friends, UITheme.Blue, TouchTarget, false, () => Ui.Show(ScreenId.Friends));
            UIFactory.CreateButton(transform, Strings.MyCharacter, UITheme.Green, TouchTarget, false, () => Ui.Show(ScreenId.Character));
            UIFactory.CreateButton(transform, Strings.Rewards, UITheme.Purple, TouchTarget, false, () => Ui.Show(ScreenId.Rewards));
            // Ebeveyn alanı küçük ve yetişkin kapısının arkasında.
            UIFactory.CreateButton(transform, Strings.Parent, UITheme.Neutral, TouchTarget, true,
                () => Ui.ShowBehindParentGate(ScreenId.ParentDashboard));
        }
    }
}
