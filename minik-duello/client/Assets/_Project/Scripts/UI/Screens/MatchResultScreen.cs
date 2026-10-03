using MinikDuello.Core;
using MinikDuello.Domain.Net;
using MinikDuello.Domain.Rewards;
using MinikDuello.Infra;
using MinikDuello.Services.Api;
using MinikDuello.Services.Managers;
using MinikDuello.Services.Realtime;
using UnityEngine;
using UnityEngine.UI;

namespace MinikDuello.UI
{
    /// <summary>
    /// Maç sonucu. Hiç kimse aşağılanmaz: kazanan "Harika oynadın!", diğeri "Çok güzel oynadın!" der; iki oyuncu da ödül alır.
    /// </summary>
    public sealed class MatchResultScreen : ScreenBase
    {
        private RectTransform content;

        protected override void Build()
        {
            content = UIFactory.CreateColumn(transform, UITheme.Spacing);
        }

        protected override void OnShown()
        {
            Render();
            RunAsync(async () =>
            {
                await ServiceLocator.Get<PlayerManager>().RefreshAsync();
                await ServiceLocator.Get<RewardManager>().RefreshInventoryAsync();
            });
        }

        public override bool OnBackPressed()
        {
            ServiceLocator.Get<MatchSession>().Reset();
            Ui.GoHome();
            return true;
        }

        private void Render()
        {
            UIFactory.ClearChildren(content);
            var match = ServiceLocator.Get<MatchSession>();
            var auth = ServiceLocator.Get<AuthSession>();
            FinishedDto result = match.LastResult ?? new FinishedDto { Reason = "connectionLost" };
            OutcomeDto mine = result.Outcomes.Find(o => o.PlayerId == auth.PlayerId);
            bool coop = result.Coop != null;
            bool happy = mine != null && (coop ? result.Coop.Success : mine.IsWinner);

            string title;
            if (result.Reason != "completed" || mine == null) title = Strings.FriendLeft;
            else if (coop) title = result.Coop.Success ? Strings.TeamWon : Strings.TeamTried;
            else title = mine.IsWinner ? Strings.YouWon : Strings.NiceGame;

            UIFactory.CreateLabel(content, title, UITheme.TitleFontSize - 6, UITheme.TextDark, UITheme.ContentWidth);
            // Kazanan altın, diğeri açık yıldızla anılır: ikisi de ödüllü.
            RectTransform stars = UIKit.StarsRow(content, happy ? 3 : 2, 3, 170f);
            if (happy && Effects.Instance != null)
            {
                ServiceLocator.Get<AudioManager>().Play(Sfx.Win);
                Effects.Instance.BurstAtCenter(false);
            }

            if (mine != null)
            {
                UIFactory.CreateLabel(content, mine.Score + " puan", UITheme.BodyFontSize, UITheme.TextDark);
                RectTransform chips = UIFactory.CreateRow(content, 14f, 80f);
                UIKit.StarChip(chips, "+" + mine.StarsAwarded, 260f);
                UIKit.CoinChip(chips, "+" + mine.CoinsAwarded, 260f);
            }
            foreach (string id in result.NewRewards)
            {
                RewardDef def = RewardCatalog.Find(id);
                UIKit.StarChip(content, "Yeni: " + (def != null ? def.DisplayName : id), 700f, UITheme.BodyFontSize - 8);
            }
            if (stars == null) return;

            bool canReplay = Nav.SelectedFriend != null && result.Reason == "completed";
            if (canReplay) UIFactory.CreateButton(content, Strings.PlayAgain, UITheme.Primary, TouchTarget, false, Replay);
            UIFactory.CreateButton(content, Strings.Menu, UITheme.Neutral, TouchTarget, false, () =>
            {
                match.Reset();
                Ui.GoHome();
            });
        }

        private void Replay()
        {
            var match = ServiceLocator.Get<MatchSession>();
            match.Reset();
            if (!match.SendInvite(Nav.SelectedFriend.PlayerId, Nav.SelectedMode))
            {
                Ui.Toast(Strings.OfflineNote);
                return;
            }
            Ui.ShowDialog(Strings.WaitingFriend, string.Empty, new DialogButton("VAZGEÇ", UITheme.Neutral, () => match.Reset()));
        }
    }
}
