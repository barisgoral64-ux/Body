using System;
using System.Threading.Tasks;
using MinikDuello.Core;
using MinikDuello.Domain.Net;
using MinikDuello.Infra;
using MinikDuello.Services;
using MinikDuello.Services.Api;
using MinikDuello.Services.Managers;
using MinikDuello.Services.Realtime;
using MinikDuello.Services.Save;
using MinikDuello.UI;
using UnityEngine;

namespace MinikDuello.Game
{
    /// <summary>
    /// Uygulama düzeyi akış denetleyicisi: ağ olaylarını gezinmeye çevirir (davet penceresi, lobi, maç, sonuç),
    /// ebeveyn süre sınırını uygular, gerçek zamanlı bağlantıyı ebeveyn izinlerine göre açar/kapatır.
    /// </summary>
    public sealed class AppController : MonoBehaviour
    {
        private const float LimitCheckSeconds = 5f;
        private const float HousekeepingSeconds = 60f;

        private AppSettings settings;
        private UIManager ui;
        private AuthSession auth;
        private PlayerManager player;
        private RewardManager rewards;
        private FriendManager friends;
        private ParentControlManager parent;
        private LevelManager levels;
        private PlaytimeLimiter limiter;
        private SaveManager save;
        private RealtimeClient realtime;
        private MatchSession match;
        private ConnectivityState connectivity;
        private IScheduler scheduler;

        private float limitTimer;
        private float housekeepingTimer;
        private string activeInviteId;
        private MatchPhase lastPhase = MatchPhase.Idle;
        private bool booted;

        public void Initialize(AppSettings appSettings)
        {
            settings = appSettings;
            ui = ServiceLocator.Get<UIManager>();
            auth = ServiceLocator.Get<AuthSession>();
            player = ServiceLocator.Get<PlayerManager>();
            rewards = ServiceLocator.Get<RewardManager>();
            friends = ServiceLocator.Get<FriendManager>();
            parent = ServiceLocator.Get<ParentControlManager>();
            levels = ServiceLocator.Get<LevelManager>();
            limiter = ServiceLocator.Get<PlaytimeLimiter>();
            save = ServiceLocator.Get<SaveManager>();
            realtime = ServiceLocator.Get<RealtimeClient>();
            match = ServiceLocator.Get<MatchSession>();
            connectivity = ServiceLocator.Get<ConnectivityState>();
            scheduler = ServiceLocator.Get<IScheduler>();

            // Çevrimdışıyken de süre sınırı ve ses ayarı uygulanır; sosyal özellikler güvenli varsayılanla KAPALI kalır.
            parent.Settings.DailyLimitMinutes = save.Data.CachedDailyLimit;
            parent.Settings.SoundEnabled = save.Data.CachedSoundEnabled;

            EventBus.Subscribe<ParentSettingsChanged>(OnParentSettingsChanged);
            realtime.MessageReceived += OnRealtimeMessage;
            realtime.Replaced += () => ui.Toast("Oyun başka bir cihazda açıldı.");
            match.InviteReceived += OnInviteReceived;
            match.RoomEntered += OnRoomEntered;
            match.RoundStarted += OnRoundStarted;
            match.MatchFinished += OnMatchFinished;
            match.ReconnectFailed += OnReconnectFailed;
            match.PhaseChanged += OnPhaseChanged;
            connectivity.Changed += OnConnectivityChanged;
            ui.ScreenShown += OnScreenShown;

            Boot();
        }

        private async void Boot()
        {
            try
            {
                await auth.EnsureRegisteredAsync();
                await RefreshAllAsync();
            }
            catch (Exception exception)
            {
                Log.Error("App", exception);
            }
            booted = true;
            ApplyRealtimePolicy();
            ui.ScreenShown -= OnScreenShown;
            ui.ScreenShown += OnScreenShown;
            Log.Info("App", "Başlatma tamamlandı. Çevrimiçi: " + connectivity.IsOnline);
        }

        private async Task RefreshAllAsync()
        {
            await levels.FlushPendingAsync();
            await player.RefreshAsync();
            await parent.RefreshAsync();
            await rewards.RefreshInventoryAsync();
            await levels.SyncFromServerAsync();
            ServiceLocator.Get<AudioManager>().RefreshVolumes();
        }

        // --- Döngü ---

        private void Update()
        {
            if (!booted) return;

            if (Application.isFocused && !ui.IsShowing(ScreenId.TimeUp)) limiter.AddPlaySeconds(Time.unscaledDeltaTime);

            limitTimer += Time.unscaledDeltaTime;
            if (limitTimer >= LimitCheckSeconds)
            {
                limitTimer = 0f;
                EnforceTimeLimit();
            }

            housekeepingTimer += Time.unscaledDeltaTime;
            if (housekeepingTimer >= HousekeepingSeconds)
            {
                housekeepingTimer = 0f;
                RunSafe(async () =>
                {
                    if (levels.PendingCount > 0) await levels.FlushPendingAsync();
                });
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused && booted) RunSafe(async () => { await levels.FlushPendingAsync(); await player.RefreshAsync(); await parent.RefreshAsync(); });
        }

        private void EnforceTimeLimit()
        {
            int limit = parent.Settings.DailyLimitMinutes;
            LimitState state = limiter.State(limit);
            if (state == LimitState.Exceeded && !ui.IsShowing(ScreenId.TimeUp))
            {
                if (match.InMatch) match.Leave();
                ui.SetBlockingMessage(null);
                ui.ForceScreen(ScreenId.TimeUp);
            }
            else if (state != LimitState.Exceeded && ui.IsShowing(ScreenId.TimeUp))
            {
                ui.GoHome();
            }
        }

        // --- Ebeveyn ayarları ve gerçek zamanlı bağlantı ---

        private void OnParentSettingsChanged(ParentSettingsChanged _)
        {
            save.Update(d =>
            {
                d.CachedDailyLimit = parent.Settings.DailyLimitMinutes;
                d.CachedSoundEnabled = parent.Settings.SoundEnabled;
            });
            ServiceLocator.Get<AudioManager>().RefreshVolumes();
            ApplyRealtimePolicy();
        }

        private void ApplyRealtimePolicy()
        {
            if (parent.Settings.FriendsEnabled && auth.HasSession) realtime.Start();
            else
            {
                realtime.Stop();
                if (match.InMatch) match.Reset();
            }
        }

        private void OnConnectivityChanged(bool online)
        {
            if (online && booted) RunSafe(RefreshAllAsync);
        }

        private void OnScreenShown(ScreenId id)
        {
            // Ana menüye her dönüşte arka planda hafifçe tazele (ağ yoksa sessizce geçer).
            if (id == ScreenId.MainMenu && booted) RunSafe(async () => { await player.RefreshAsync(); await levels.FlushPendingAsync(); });
        }

        // --- Gerçek zamanlı mesajlar ---

        private void OnRealtimeMessage(ServerMessage message)
        {
            switch (message.Type)
            {
                case ServerTypes.PresenceUpdate:
                    friends.ApplyPresence(message.As<PresenceUpdateDto>());
                    break;
                case ServerTypes.FriendEvent:
                    FriendEventDto e = message.As<FriendEventDto>();
                    if (e.Type == "friend.requestReceived") ui.Toast("Yeni arkadaş isteği var! Bir yetişkine göster.");
                    else if (e.Type == "friend.added")
                    {
                        ui.Toast("Yeni bir arkadaşın var!");
                        RunSafe(async () => { await friends.RefreshAsync(); });
                    }
                    break;
            }
        }

        // --- Davet ve maç akışı ---

        private void OnInviteReceived(InviteReceivedDto invite)
        {
            bool busy = match.InMatch || match.Phase == MatchPhase.Inviting
                || ui.IsShowing(ScreenId.TimeUp) || ui.IsShowing(ScreenId.ParentGate) || ui.IsShowing(ScreenId.ParentDashboard);
            if (busy)
            {
                match.RespondToInvite(invite.InviteId, false);
                return;
            }

            activeInviteId = invite.InviteId;
            string name = invite.From != null ? invite.From.Username : "Arkadaşın";
            ServiceLocator.Get<AudioManager>().Play(Sfx.Pop);
            ui.ShowDialog(name, Strings.WantsToPlay,
                new DialogButton(Strings.Accept, UITheme.Green, () => AcceptInvite(invite)),
                new DialogButton(Strings.NotNow, UITheme.Neutral, () =>
                {
                    activeInviteId = null;
                    match.RespondToInvite(invite.InviteId, false);
                }));

            string id = invite.InviteId;
            scheduler.After(30d, () =>
            {
                if (activeInviteId == id)
                {
                    activeInviteId = null;
                    ui.CloseDialog();
                }
            });
        }

        private void AcceptInvite(InviteReceivedDto invite)
        {
            activeInviteId = null;
            ui.Context.OpponentName = invite.From != null ? invite.From.Username : "Arkadaşın";
            ui.Context.OpponentCharacter = invite.From != null && !string.IsNullOrEmpty(invite.From.AvatarCharacter) ? invite.From.AvatarCharacter : "panda";
            ui.Context.CoopSelected = GameModes.IsCoop(invite.Mode);
            ui.Context.SelectedMode = invite.Mode;
            ui.Context.SelectedFriend = invite.From == null ? null : friends.Friends.FirstOrDefault(invite.From.PlayerId);
            if (!match.RespondToInvite(invite.InviteId, true)) ui.Toast(Strings.OfflineNote);
        }

        private void OnRoomEntered(RoomStateDto state)
        {
            ui.CloseDialog();
            if (state.State == "playing" && match.Phase == MatchPhase.Playing)
            {
                if (!ui.IsShowing(ScreenId.MatchGame)) ui.Show(ScreenId.MatchGame);
                return;
            }
            if (!ui.IsShowing(ScreenId.Lobby) && !ui.IsShowing(ScreenId.MatchGame)) ui.Show(ScreenId.Lobby);
        }

        private void OnRoundStarted(RoundDto round)
        {
            ui.CloseDialog();
            if (ui.IsShowing(ScreenId.MatchGame)) return;
            if (ui.IsShowing(ScreenId.Lobby)) ui.Replace(ScreenId.MatchGame);
            else ui.Show(ScreenId.MatchGame);
        }

        private void OnMatchFinished(FinishedDto result) => ShowResult();

        private void OnReconnectFailed() => ShowResult();

        private void ShowResult()
        {
            ui.CloseDialog();
            ui.SetBlockingMessage(null);
            if (ui.IsShowing(ScreenId.MatchResult)) return;
            if (ui.IsShowing(ScreenId.MatchGame) || ui.IsShowing(ScreenId.Lobby)) ui.Replace(ScreenId.MatchResult);
            else ui.Show(ScreenId.MatchResult);
        }

        private void OnPhaseChanged(MatchPhase phase)
        {
            if (phase == MatchPhase.Reconnecting) ui.SetBlockingMessage(Strings.Reconnecting);
            else if (lastPhase == MatchPhase.Reconnecting) ui.SetBlockingMessage(null);
            lastPhase = phase;
        }

        private async void RunSafe(Func<Task> work)
        {
            try
            {
                await work();
            }
            catch (Exception exception)
            {
                Log.Error("App", exception);
            }
        }
    }

    internal static class FriendListExtensions
    {
        public static FriendDto FirstOrDefault(this System.Collections.Generic.IReadOnlyList<FriendDto> list, string playerId)
        {
            foreach (FriendDto f in list)
            {
                if (f.PlayerId == playerId) return f;
            }
            return null;
        }
    }
}
