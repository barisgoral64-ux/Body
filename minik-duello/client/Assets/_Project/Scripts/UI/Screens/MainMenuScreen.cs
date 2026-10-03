using System.Collections.Generic;
using MinikDuello.Core;
using MinikDuello.Services;
using MinikDuello.Services.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace MinikDuello.UI
{
    public sealed class MainMenuScreen : ScreenBase
    {
        private RectTransform header;
        private Text offlineNote;
        private Text versionLabel;
        private GameObject dailyButton;
        private Button friendsButton;

        protected override void Build()
        {
            header = UIFactory.CreateRow(transform, UITheme.Spacing, 190f);
            UIFactory.CreateLabel(transform, Strings.GameTitle, UITheme.TitleFontSize, UITheme.TextDark);
            offlineNote = UIFactory.CreateLabel(transform, Strings.OfflineNote, UITheme.SmallFontSize, UITheme.Neutral);

            UIFactory.CreateButton(transform, Strings.Play, UITheme.Primary, TouchTarget, false, () => Ui.Show(ScreenId.PlayMenu));
            friendsButton = UIFactory.CreateButton(transform, Strings.Friends, UITheme.Blue, TouchTarget, false, () => Ui.Show(ScreenId.Friends));
            UIFactory.CreateButton(transform, Strings.MyCharacter, UITheme.Green, TouchTarget, false, () => Ui.Show(ScreenId.Character));
            UIFactory.CreateButton(transform, Strings.Rewards, UITheme.Purple, TouchTarget, false, () => Ui.Show(ScreenId.Rewards));

            versionLabel = UIFactory.CreateLabel(transform, string.Empty, UITheme.SmallFontSize, UITheme.Neutral, UITheme.ContentWidth);
            RectTransform row = UIFactory.CreateRow(transform, UITheme.Spacing, TouchTarget);
            dailyButton = UIFactory.CreateButton(row, Strings.Daily, UITheme.Yellow, TouchTarget, true, () => Ui.Show(ScreenId.DailyReward)).gameObject;
            // Ebeveyn alanı küçük ve yetişkin kapısının arkasında.
            UIFactory.CreateButton(row, Strings.Parent, UITheme.Neutral, TouchTarget, true, () => Ui.ShowBehindParentGate(ScreenId.ParentDashboard));
        }

        protected override void OnShown()
        {
            BuildHeader();
            versionLabel.text = Strings.Version + " " + Config.AppVersion;
            var connectivity = ServiceLocator.Get<ConnectivityState>();
            offlineNote.gameObject.SetActive(!connectivity.IsOnline);
            dailyButton.SetActive(false);
            RunAsync(async () =>
            {
                var rewards = ServiceLocator.Get<RewardManager>();
                var status = await rewards.DailyStatusAsync();
                if (IsAlive && status.IsOk) dailyButton.SetActive(status.Value.CanClaim);
            });
        }

        private new void BuildHeader()
        {
            UIFactory.ClearChildren(header);
            var player = ServiceLocator.Get<PlayerManager>();
            var characters = ServiceLocator.Get<CharacterManager>();
            Dictionary<string, string> equipped = characters.EquippedBySlot();

            var tap = UIFactory.CreateButton(header, string.Empty, new Color(1f, 1f, 1f, 0.01f), 150f, true, () => Ui.Show(ScreenId.Profile));
            UIFactory.ClearChildren(tap.transform);
            var tapLe = tap.GetComponent<LayoutElement>();
            tapLe.preferredWidth = 170f;
            tapLe.preferredHeight = 170f;
            RectTransform avatar = UIKit.Avatar(tap.transform, player.Character, equipped, 160f);
            avatar.anchorMin = avatar.anchorMax = new Vector2(0.5f, 0.5f);
            avatar.anchoredPosition = Vector2.zero;

            RectTransform info = UIFactory.CreateColumn(header, 6f, 640f);
            UIFactory.CreateLabel(info, player.Username, UITheme.BodyFontSize, UITheme.TextDark, 640f, TextAnchor.MiddleLeft);
            RectTransform chips = UIFactory.CreateRow(info, 10f, 64f, TextAnchor.MiddleLeft);
            UIKit.Chip(chips, Strings.Level + " " + player.Level, UITheme.Card, 190f);
            UIKit.StarChip(chips, player.Stars.ToString(), 190f);
            UIKit.CoinChip(chips, player.Coins.ToString(), 190f);
        }
    }
}
