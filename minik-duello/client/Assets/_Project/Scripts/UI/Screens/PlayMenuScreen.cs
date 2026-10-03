using MinikDuello.Core;
using MinikDuello.Services.Managers;

namespace MinikDuello.UI
{
    public sealed class PlayMenuScreen : ScreenBase
    {
        protected override void Build()
        {
            BuildHeader(Strings.Play);
            UIFactory.CreateButton(transform, Strings.SinglePlayer, UITheme.Primary, TouchTarget, false, () => Ui.Show(ScreenId.WorldSelect));
            UIFactory.CreateButton(transform, Strings.PlayWithFriend, UITheme.Blue, TouchTarget, false, () => OpenFriendPlay(false));
            UIFactory.CreateButton(transform, Strings.PlayTogether, UITheme.Green, TouchTarget, false, () => OpenFriendPlay(true));
        }

        private void OpenFriendPlay(bool coop)
        {
            Nav.CoopSelected = coop;
            Ui.Show(ScreenId.FriendPlay);
        }
    }
}
