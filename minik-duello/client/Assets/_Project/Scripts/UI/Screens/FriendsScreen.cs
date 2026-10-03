using MinikDuello.Core;
using MinikDuello.Domain.Net;
using MinikDuello.Services.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace MinikDuello.UI
{
    /// <summary>Arkadaş listesi: avatar, ad, çevrimiçi durumu ve davet düğmesi. Ekleme/istekler yetişkin kapısının arkasında.</summary>
    public sealed class FriendsScreen : ScreenBase
    {
        private RectTransform actions;
        private RectTransform list;
        private Text info;

        protected override void Build()
        {
            BuildHeader(Strings.Friends);
            info = UIFactory.CreateLabel(transform, string.Empty, UITheme.BodyFontSize - 6, UITheme.Neutral, UITheme.ContentWidth);
            actions = UIFactory.CreateRow(transform, 14f, TouchTarget);
            list = UIFactory.CreateScroll(transform);
        }

        protected override void OnShown()
        {
            EventBus.Subscribe<FriendsChanged>(OnFriendsChanged);
            Render();
            RunAsync(async () =>
            {
                var parent = ServiceLocator.Get<ParentControlManager>();
                await parent.RefreshAsync();
                if (parent.SocialAvailable) await ServiceLocator.Get<FriendManager>().RefreshAsync();
                if (IsAlive) Render();
            });
        }

        protected override void OnHidden() => EventBus.Unsubscribe<FriendsChanged>(OnFriendsChanged);

        private void OnFriendsChanged(FriendsChanged _)
        {
            if (IsAlive) Render();
        }

        private void Render()
        {
            UIFactory.ClearChildren(actions);
            UIFactory.ClearChildren(list);
            var parent = ServiceLocator.Get<ParentControlManager>();
            var friends = ServiceLocator.Get<FriendManager>();

            if (!parent.SocialAvailable)
            {
                info.text = Strings.ParentNeedsToEnable;
                UIFactory.CreateButton(actions, Strings.Parent, UITheme.Neutral, TouchTarget, true, () => Ui.ShowBehindParentGate(ScreenId.ParentDashboard));
                return;
            }

            info.text = friends.Friends.Count == 0 ? "Arkadaş kodunla yeni arkadaşlar ekleyebilirsin!" : string.Empty;
            UIFactory.CreateButton(actions, "EKLE", UITheme.Green, TouchTarget, true, () => Ui.ShowBehindParentGate(ScreenId.AddFriend));
            UIFactory.CreateButton(actions, Strings.Requests, UITheme.Primary, TouchTarget, true, () => Ui.ShowBehindParentGate(ScreenId.Requests));
            UIFactory.CreateButton(actions, Strings.ThisWeek, UITheme.Purple, TouchTarget, true, () => Ui.Show(ScreenId.Weekly));

            foreach (FriendDto friend in friends.Friends) BuildRow(friend, parent);
        }

        private void BuildRow(FriendDto friend, ParentControlManager parent)
        {
            Image row = UIFactory.CreatePanel(list, "Friend_" + friend.PlayerId, UITheme.Card);
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = UITheme.ContentWidth - 20f;
            le.preferredHeight = 170f;
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 12, 12);
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            UIKit.Avatar(row.transform, friend.AvatarCharacter ?? "panda", null, 120f);

            RectTransform texts = UIFactory.CreateColumn(row.transform, 4f, 400f);
            UIFactory.CreateLabel(texts, friend.Username, UITheme.BodyFontSize - 8, UITheme.TextDark, 400f, TextAnchor.MiddleLeft);
            RectTransform status = UIFactory.CreateRow(texts, 10f, 44f, TextAnchor.MiddleLeft);
            Image dot = UIFactory.CreateImage(status, "Dot", PresenceColor(friend.Presence));
            dot.sprite = SpriteFactory.Shape("circle");
            var dotLe = dot.gameObject.AddComponent<LayoutElement>();
            dotLe.preferredWidth = 30f;
            dotLe.preferredHeight = 30f;
            UIFactory.CreateLabel(status, PresenceText(friend.Presence), UITheme.SmallFontSize, UITheme.Neutral, 300f, TextAnchor.MiddleLeft);

            bool canInvite = friend.Presence == "online" && parent.MultiplayerAvailable && parent.Settings.GameInvitationsEnabled;
            FriendDto captured = friend;
            Button invite = UIFactory.CreateButton(row.transform, "DAVET", canInvite ? UITheme.Primary : UITheme.Locked, TouchTarget, true, () =>
            {
                if (!canInvite)
                {
                    Ui.Toast(friend.Presence == "online" ? Strings.ParentNeedsToEnable : "Şu an oynayamıyor.");
                    return;
                }
                Nav.SelectedFriend = captured;
                Nav.OpponentName = captured.Username;
                Nav.OpponentCharacter = captured.AvatarCharacter ?? "panda";
                Nav.CoopSelected = false;
                Ui.Show(ScreenId.FriendPlay);
            });
            invite.GetComponent<LayoutElement>().preferredWidth = 190f;
        }

        public static Color PresenceColor(string presence)
        {
            switch (presence)
            {
                case "online": return UITheme.Green;
                case "playing": return UITheme.Primary;
                default: return UITheme.Locked;
            }
        }

        public static string PresenceText(string presence)
        {
            switch (presence)
            {
                case "online": return Strings.Online;
                case "playing": return Strings.Playing;
                default: return Strings.Offline;
            }
        }
    }
}
