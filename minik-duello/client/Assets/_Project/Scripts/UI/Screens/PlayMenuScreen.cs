using MinikDuello.Core;

namespace MinikDuello.UI
{
    public sealed class PlayMenuScreen : ScreenBase
    {
        protected override void Build()
        {
            UIFactory.CreateLabel(transform, Strings.Play, UITheme.TitleFontSize, UITheme.TextDark);
            UIFactory.CreateButton(transform, Strings.SinglePlayer, UITheme.Primary, TouchTarget, false, () => Ui.Show(ScreenId.SinglePlayer));
            UIFactory.CreateButton(transform, Strings.PlayWithFriend, UITheme.Blue, TouchTarget, false, () => Ui.Show(ScreenId.FriendPlay));
            UIFactory.CreateButton(transform, Strings.PlayTogether, UITheme.Green, TouchTarget, false, () => Ui.Show(ScreenId.CoopPlay));
            UIFactory.CreateButton(transform, Strings.Back, UITheme.Neutral, TouchTarget, true, Ui.Back);
        }
    }
}
