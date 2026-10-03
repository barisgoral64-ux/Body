using MinikDuello.Core;

namespace MinikDuello.UI
{
    /// <summary>İlerleyen fazlarda gerçek ekranla değiştirilecek geçici ekran: başlık + "Yakında!" + geri.</summary>
    public sealed class PlaceholderScreen : ScreenBase
    {
        protected override void Build()
        {
            UIFactory.CreateLabel(transform, TitleFor(Id), UITheme.TitleFontSize, UITheme.TextDark);
            UIFactory.CreateLabel(transform, Strings.ComingSoon, UITheme.BodyFontSize, UITheme.TextDark);
            UIFactory.CreateButton(transform, Strings.Back, UITheme.Neutral, TouchTarget, true, Ui.Back);
        }

        private static string TitleFor(ScreenId id)
        {
            switch (id)
            {
                case ScreenId.SinglePlayer: return Strings.SinglePlayer;
                case ScreenId.FriendPlay: return Strings.PlayWithFriend;
                case ScreenId.CoopPlay: return Strings.PlayTogether;
                case ScreenId.Friends: return Strings.Friends;
                case ScreenId.Character: return Strings.MyCharacter;
                case ScreenId.Rewards: return Strings.Rewards;
                case ScreenId.ParentDashboard: return Strings.ParentArea;
                default: return Strings.GameTitle;
            }
        }
    }
}
