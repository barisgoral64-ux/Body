using System;
using MinikDuello.Core;
using MinikDuello.Domain.Net;
using MinikDuello.Infra;
using MinikDuello.Services;
using MinikDuello.Services.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace MinikDuello.UI
{
    /// <summary>
    /// Ebeveyn paneli. Yetişkin kapısından sonra ayrıca 4 haneli PIN gerekir (PIN yoksa önce belirlenir).
    /// PIN yalnızca bellekte tutulur ve panel kapanınca silinir; doğrulama ve kilitleme sunucudadır.
    /// </summary>
    public sealed class ParentDashboardScreen : ScreenBase
    {
        private static readonly int[] LimitOptions = { 0, 15, 30, 45, 60, 90, 120 };
        private static readonly int[] VolumeOptions = { 0, 35, 70, 100 };

        private enum Step
        {
            Loading,
            CreatePin,
            ConfirmPin,
            EnterPin,
            Dashboard,
            ChangeCurrent,
            ChangeNew,
            ChangeConfirm
        }

        private RectTransform content;
        private Step step = Step.Loading;
        private string entry = string.Empty;
        private string firstPin;
        private string currentPin;
        private Text entryLabel;

        protected override void Build()
        {
            BuildHeader(Strings.ParentArea);
            content = UIFactory.CreateScroll(transform);
        }

        protected override void OnShown()
        {
            Nav.ClearParent();
            entry = string.Empty;
            step = Step.Loading;
            RenderLoading();
            RunAsync(async () =>
            {
                var parent = ServiceLocator.Get<ParentControlManager>();
                var result = await parent.RefreshAsync();
                if (!IsAlive) return;
                if (!result.IsOk)
                {
                    UIFactory.ClearChildren(content);
                    UIFactory.CreateLabel(content, Messages.For(result.Error), UITheme.BodyFontSize, UITheme.Neutral, UITheme.ContentWidth);
                    return;
                }
                GoTo(parent.Settings.HasPin ? Step.EnterPin : Step.CreatePin);
            });
        }

        protected override void OnHidden() => Nav.ClearParent();

        private void RenderLoading()
        {
            UIFactory.ClearChildren(content);
            UIFactory.CreateLabel(content, "...", UITheme.TitleFontSize, UITheme.Neutral, UITheme.ContentWidth);
        }

        private void GoTo(Step next)
        {
            step = next;
            entry = string.Empty;
            if (next == Step.Dashboard) RenderDashboard();
            else RenderPinEntry();
        }

        // --- PIN girişi ---

        private void RenderPinEntry()
        {
            UIFactory.ClearChildren(content);
            string title;
            switch (step)
            {
                case Step.CreatePin: title = Strings.SetPinFirst; break;
                case Step.ConfirmPin:
                case Step.ChangeConfirm: title = "PIN'i tekrar girin"; break;
                case Step.ChangeCurrent: title = "Mevcut PIN"; break;
                case Step.ChangeNew: title = "Yeni PIN"; break;
                default: title = Strings.EnterPin; break;
            }
            UIFactory.CreateLabel(content, title, UITheme.BodyFontSize, UITheme.TextDark, UITheme.ContentWidth);
            entryLabel = UIFactory.CreateLabel(content, NumberPad.Masked(0, Config.ParentPinLength), UITheme.TitleFontSize, UITheme.Primary, UITheme.ContentWidth);
            NumberPad.Build(content, TouchTarget, OnDigit, OnErase);
        }

        private void OnDigit(int digit)
        {
            if (entry.Length >= Config.ParentPinLength) return;
            entry += digit;
            entryLabel.text = NumberPad.Masked(entry.Length, Config.ParentPinLength);
            if (entry.Length == Config.ParentPinLength) OnPinComplete(entry);
        }

        private void OnErase()
        {
            if (entry.Length == 0) return;
            entry = entry.Substring(0, entry.Length - 1);
            entryLabel.text = NumberPad.Masked(entry.Length, Config.ParentPinLength);
        }

        private void OnPinComplete(string pin)
        {
            var parent = ServiceLocator.Get<ParentControlManager>();
            switch (step)
            {
                case Step.CreatePin:
                    firstPin = pin;
                    GoTo(Step.ConfirmPin);
                    break;
                case Step.ConfirmPin:
                    if (pin != firstPin)
                    {
                        Ui.Toast("PIN'ler aynı değil. Tekrar deneyelim.");
                        GoTo(Step.CreatePin);
                        break;
                    }
                    RunAsync(async () =>
                    {
                        var result = await parent.SetPinAsync(pin, null);
                        if (!IsAlive) return;
                        if (result.IsOk) Unlock(pin);
                        else { Ui.Toast(Messages.For(result.Error)); GoTo(Step.CreatePin); }
                    }, true);
                    break;
                case Step.EnterPin:
                    RunAsync(async () =>
                    {
                        var result = await parent.VerifyPinAsync(pin);
                        if (!IsAlive) return;
                        if (result.IsOk) Unlock(pin);
                        else { Ui.Toast(Messages.For(result.Error)); GoTo(Step.EnterPin); }
                    }, true);
                    break;
                case Step.ChangeCurrent:
                    currentPin = pin;
                    GoTo(Step.ChangeNew);
                    break;
                case Step.ChangeNew:
                    firstPin = pin;
                    GoTo(Step.ChangeConfirm);
                    break;
                case Step.ChangeConfirm:
                    if (pin != firstPin)
                    {
                        Ui.Toast("PIN'ler aynı değil.");
                        GoTo(Step.ChangeNew);
                        break;
                    }
                    RunAsync(async () =>
                    {
                        var result = await parent.SetPinAsync(pin, currentPin);
                        if (!IsAlive) return;
                        if (result.IsOk) { Unlock(pin); Ui.Toast("PIN değişti."); }
                        else { Ui.Toast(Messages.For(result.Error)); GoTo(Step.Dashboard); }
                    }, true);
                    break;
            }
        }

        private void Unlock(string pin)
        {
            Nav.ParentPin = pin;
            GoTo(Step.Dashboard);
        }

        // --- Panel ---

        private void RenderDashboard()
        {
            UIFactory.ClearChildren(content);
            var parent = ServiceLocator.Get<ParentControlManager>();
            var friends = ServiceLocator.Get<FriendManager>();
            var limiter = ServiceLocator.Get<PlaytimeLimiter>();
            var save = ServiceLocator.Get<MinikDuello.Services.Save.SaveManager>();
            ParentSettingsDto s = parent.Settings;

            Section("Arkadaşlık ve çok oyunculu");
            Toggle("Arkadaşlık", s.FriendsEnabled, v => new { friendsEnabled = v });
            Toggle("Çok oyunculu", s.MultiplayerEnabled, v => new { multiplayerEnabled = v });
            Toggle("Çevrimiçi görünürlük", s.OnlineStatusVisible, v => new { onlineStatusVisible = v });
            Toggle("Oyun davetleri", s.GameInvitationsEnabled, v => new { gameInvitationsEnabled = v });
            UIFactory.CreateButton(content, "GELEN İSTEKLER", UITheme.Primary, TouchTarget, true, () => Ui.Show(ScreenId.Requests));

            Section("Süre ve ses");
            string limitText = s.DailyLimitMinutes <= 0 ? "Sınırsız" : s.DailyLimitMinutes + " dk";
            UIFactory.CreateButton(content, "Günlük süre: " + limitText, UITheme.Blue, TouchTarget, true, () => CycleLimit(s.DailyLimitMinutes));
            UIFactory.CreateLabel(content, "Bugün oynanan: " + Mathf.RoundToInt((float)limiter.MinutesToday()) + " dk", UITheme.SmallFontSize + 2, UITheme.Neutral, UITheme.ContentWidth);
            Toggle("Ses", s.SoundEnabled, v => new { soundEnabled = v });
            UIFactory.CreateButton(content, "Müzik: %" + save.Data.MusicVolume, UITheme.Blue, TouchTarget, true, () => CycleVolume(true, save));
            UIFactory.CreateButton(content, "Efekt: %" + save.Data.SfxVolume, UITheme.Blue, TouchTarget, true, () => CycleVolume(false, save));

            Section("Arkadaşlarım");
            if (friends.Friends.Count == 0) UIFactory.CreateLabel(content, "Henüz arkadaş yok.", UITheme.SmallFontSize + 4, UITheme.Neutral, UITheme.ContentWidth);
            foreach (FriendDto friend in friends.Friends) FriendRow(friend);
            if (s.FriendsEnabled && friends.Friends.Count == 0)
            {
                RunAsync(async () =>
                {
                    await friends.RefreshAsync();
                    if (IsAlive && step == Step.Dashboard && friends.Friends.Count > 0) RenderDashboard();
                });
            }

            Section("Güvenlik");
            UIFactory.CreateButton(content, "YENİ ARKADAŞ KODU", UITheme.Purple, TouchTarget, true, RegenerateCode);
            UIFactory.CreateButton(content, "PIN DEĞİŞTİR", UITheme.Purple, TouchTarget, true, () => GoTo(Step.ChangeCurrent));
            UIFactory.CreateSpacer(content, 20f);
            UIFactory.CreateButton(content, "KAPAT", UITheme.Neutral, TouchTarget, false, Ui.GoHome);
            UIFactory.CreateSpacer(content, 40f);
        }

        private void Section(string title)
        {
            UIFactory.CreateSpacer(content, 10f);
            UIFactory.CreateLabel(content, title, UITheme.BodyFontSize, UITheme.TextDark, UITheme.ContentWidth, TextAnchor.MiddleLeft);
        }

        private void Toggle(string label, bool value, Func<bool, object> patch)
        {
            UIFactory.CreateButton(content, label + ": " + (value ? "AÇIK" : "KAPALI"), value ? UITheme.Green : UITheme.Neutral, TouchTarget, true, () => Apply(patch(!value)));
        }

        private void Apply(object patch)
        {
            RunAsync(async () =>
            {
                var result = await ServiceLocator.Get<ParentControlManager>().UpdateAsync(patch, Nav.ParentPin);
                if (!IsAlive) return;
                if (!result.IsOk) Ui.Toast(Messages.For(result.Error));
                ServiceLocator.Get<AudioManager>().RefreshVolumes();
                RenderDashboard();
            }, true);
        }

        private void CycleLimit(int current)
        {
            int index = Array.IndexOf(LimitOptions, current);
            Apply(new { dailyLimitMinutes = LimitOptions[(index + 1) % LimitOptions.Length] });
        }

        private void CycleVolume(bool music, MinikDuello.Services.Save.SaveManager save)
        {
            int current = music ? save.Data.MusicVolume : save.Data.SfxVolume;
            int next = VolumeOptions[0];
            for (int i = 0; i < VolumeOptions.Length; i++)
            {
                if (VolumeOptions[i] > current) { next = VolumeOptions[i]; break; }
            }
            save.Update(d => { if (music) d.MusicVolume = next; else d.SfxVolume = next; });
            ServiceLocator.Get<AudioManager>().RefreshVolumes();
            RenderDashboard();
        }

        private void FriendRow(FriendDto friend)
        {
            Image row = UIFactory.CreatePanel(content, "F_" + friend.PlayerId, UITheme.Card);
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = UITheme.ContentWidth - 20f;
            le.preferredHeight = 130f;
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 10, 10);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            UIFactory.CreateLabel(row.transform, friend.Username, UITheme.SmallFontSize + 6, UITheme.TextDark, 330f, TextAnchor.MiddleLeft);
            UIFactory.CreateButton(row.transform, "KALDIR", UITheme.Neutral, 90f, true, () => Confirm("Arkadaş kaldırılsın mı?", friend.Username, "KALDIR", () => RemoveFriend(friend)));
            UIFactory.CreateButton(row.transform, "ENGELLE", UITheme.Pink, 90f, true, () => Confirm("Oyuncu engellensin mi?", friend.Username, "ENGELLE", () => Block(friend)));
        }

        private void Confirm(string title, string name, string yes, Action action)
        {
            Ui.ShowDialog(title, name, new DialogButton(yes, UITheme.Pink, action), new DialogButton("VAZGEÇ", UITheme.Neutral));
        }

        private void RemoveFriend(FriendDto friend)
        {
            RunAsync(async () =>
            {
                var parent = ServiceLocator.Get<ParentControlManager>();
                var result = await parent.RemoveFriendAsync(friend.PlayerId, Nav.ParentPin);
                if (!result.IsOk) Ui.Toast(Messages.For(result.Error));
                await ServiceLocator.Get<FriendManager>().RefreshAsync();
                if (IsAlive) RenderDashboard();
            }, true);
        }

        private void Block(FriendDto friend)
        {
            RunAsync(async () =>
            {
                var parent = ServiceLocator.Get<ParentControlManager>();
                var result = await parent.BlockAsync(friend.PlayerId, Nav.ParentPin);
                Ui.Toast(result.IsOk ? "Oyuncu engellendi." : Messages.For(result.Error));
                await ServiceLocator.Get<FriendManager>().RefreshAsync();
                if (IsAlive) RenderDashboard();
            }, true);
        }

        private void RegenerateCode()
        {
            RunAsync(async () =>
            {
                var result = await ServiceLocator.Get<ParentControlManager>().RegenerateCodeAsync(Nav.ParentPin);
                if (result.IsOk)
                {
                    await ServiceLocator.Get<PlayerManager>().RefreshAsync();
                    Ui.ShowDialog("Yeni arkadaş kodu", result.Value, new DialogButton(Strings.Ok, UITheme.Green));
                }
                else
                {
                    Ui.Toast(Messages.For(result.Error));
                }
            }, true);
        }
    }
}
