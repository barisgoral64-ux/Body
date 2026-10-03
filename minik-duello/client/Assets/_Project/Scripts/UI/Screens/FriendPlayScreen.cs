using MinikDuello.Core;
using MinikDuello.Domain.Net;
using MinikDuello.Services.Managers;
using MinikDuello.Services.Realtime;
using UnityEngine;
using UnityEngine.UI;

namespace MinikDuello.UI
{
    /// <summary>Arkadaşını (çevrimiçi olanlardan) ve oyun modunu seç, davet gönder. Yarışma veya birlikte oynama.</summary>
    public sealed class FriendPlayScreen : ScreenBase
    {
        private RectTransform content;
        private MatchSession match;

        protected override void Build()
        {
            BuildHeader(Strings.PlayWithFriend);
            content = UIFactory.CreateScroll(transform);
        }

        protected override void OnShown()
        {
            match = ServiceLocator.Get<MatchSession>();
            match.InviteDeclined += OnDeclined;
            match.ErrorReceived += OnError;
            Render();
            RunAsync(async () =>
            {
                var parent = ServiceLocator.Get<ParentControlManager>();
                await parent.RefreshAsync();
                if (parent.SocialAvailable) await ServiceLocator.Get<FriendManager>().RefreshAsync();
                if (IsAlive) Render();
            });
        }

        protected override void OnHidden()
        {
            if (match == null) return;
            match.InviteDeclined -= OnDeclined;
            match.ErrorReceived -= OnError;
        }

        private void Render()
        {
            UIFactory.ClearChildren(content);
            var parent = ServiceLocator.Get<ParentControlManager>();
            var friends = ServiceLocator.Get<FriendManager>();

            if (!parent.MultiplayerAvailable || !parent.Settings.GameInvitationsEnabled)
            {
                UIFactory.CreateLabel(content, Strings.ParentNeedsToEnable, UITheme.BodyFontSize - 6, UITheme.Neutral, UITheme.ContentWidth);
                UIFactory.CreateButton(content, Strings.Parent, UITheme.Neutral, TouchTarget, true, () => Ui.ShowBehindParentGate(ScreenId.ParentDashboard));
                return;
            }

            if (Nav.SelectedFriend == null)
            {
                RenderFriendChoice(friends);
                return;
            }

            UIFactory.CreateLabel(content, Nav.SelectedFriend.Username, UITheme.TitleFontSize - 16, UITheme.TextDark, UITheme.ContentWidth);
            UIFactory.CreateLabel(content, Nav.CoopSelected ? "Birlikte oynayalım!" : "Hangi oyunu oynayalım?", UITheme.BodyFontSize - 6, UITheme.Neutral, UITheme.ContentWidth);
            string[] modes = Nav.CoopSelected ? GameModes.Cooperative : GameModes.Competitive;
            foreach (string mode in modes)
            {
                string captured = mode;
                UIFactory.CreateButton(content, ModeName(mode), Nav.CoopSelected ? UITheme.Green : UITheme.Primary, TouchTarget, false, () => Invite(captured));
            }
            UIFactory.CreateButton(content, "BAŞKA ARKADAŞ", UITheme.Neutral, TouchTarget, true, () =>
            {
                Nav.SelectedFriend = null;
                Render();
            });
        }

        private void RenderFriendChoice(FriendManager friends)
        {
            UIFactory.CreateLabel(content, "Kiminle oynayalım?", UITheme.BodyFontSize, UITheme.TextDark, UITheme.ContentWidth);
            bool any = false;
            foreach (FriendDto friend in friends.Friends)
            {
                if (friend.Presence != "online") continue;
                any = true;
                FriendDto captured = friend;
                UIFactory.CreateButton(content, friend.Username, UITheme.Blue, TouchTarget, false, () =>
                {
                    Nav.SelectedFriend = captured;
                    Nav.OpponentName = captured.Username;
                    Nav.OpponentCharacter = captured.AvatarCharacter ?? "panda";
                    Render();
                });
            }
            if (!any) UIFactory.CreateLabel(content, "Şu an çevrimiçi arkadaşın yok.", UITheme.BodyFontSize - 6, UITheme.Neutral, UITheme.ContentWidth);
        }

        private void Invite(string mode)
        {
            Nav.SelectedMode = mode;
            if (!match.SendInvite(Nav.SelectedFriend.PlayerId, mode))
            {
                Ui.Toast(Strings.OfflineNote);
                return;
            }
            ServiceLocator.Get<MinikDuello.Infra.AudioManager>().Play(MinikDuello.Infra.Sfx.Whoosh);
            Ui.ShowDialog(Strings.WaitingFriend, string.Empty, new DialogButton("VAZGEÇ", UITheme.Neutral, () => match.Reset()));
        }

        private void OnDeclined(string inviteId)
        {
            Ui.CloseDialog();
            Ui.Toast("Şu an oynayamıyor. Biraz sonra tekrar deneyelim!");
        }

        private void OnError(string code)
        {
            Ui.CloseDialog();
            match.Reset();
            Ui.Toast(Messages.ForServer(code));
        }

        public static string ModeName(string mode)
        {
            switch (mode)
            {
                case GameModes.ColorRace: return "RENK YARIŞI";
                case GameModes.ShapeRace: return "ŞEKİL YARIŞI";
                case GameModes.NumberRace: return "SAYI YARIŞI";
                case GameModes.MemoryDuel: return "HAFIZA DÜELLOSU";
                case GameModes.PuzzleRace: return "PUZZLE YARIŞI";
                case GameModes.StarCollect: return "YILDIZ TOPLAMA";
                case GameModes.MixedMatch: return "KARIŞIK MAÇ";
                case GameModes.CoopStars: return "YILDIZ TOPLAYALIM";
                case GameModes.CoopPuzzle: return "BİRLİKTE PUZZLE";
                default: return mode;
            }
        }
    }
}
