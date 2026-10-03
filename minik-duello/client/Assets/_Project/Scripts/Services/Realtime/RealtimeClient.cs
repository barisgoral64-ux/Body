using System;
using MinikDuello.Core;
using MinikDuello.Domain.Net;
using MinikDuello.Services.Api;

namespace MinikDuello.Services.Realtime
{
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected
    }

    /// <summary>
    /// WebSocket yaşam döngüsü: auth el sıkışması, kalp atışı, üssel geri çekilmeli yeniden bağlanma.
    /// "Connected" durumu yalnızca sunucu auth.ok döndükten sonra olur.
    /// </summary>
    public sealed class RealtimeClient
    {
        public const int CloseUnauthorized = 4401;
        public const int CloseReplaced = 4000;
        public const int CloseFlood = 4429;

        private readonly AppSettings settings;
        private readonly IWebSocketTransport ws;
        private readonly AuthSession auth;
        private readonly IScheduler scheduler;

        private IDisposable heartbeat;
        private IDisposable authTimeout;
        private IDisposable reconnectTimer;
        private bool wantConnected;
        private int attempt;

        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
        public bool IsConnected => State == ConnectionState.Connected;

        public event Action<ServerMessage> MessageReceived;
        public event Action<ConnectionState> StateChanged;
        /// <summary>Aynı hesap başka cihazda bağlandı; otomatik yeniden bağlanma yapılmaz.</summary>
        public event Action Replaced;

        public RealtimeClient(AppSettings settings, IWebSocketTransport ws, AuthSession auth, IScheduler scheduler)
        {
            this.settings = settings;
            this.ws = ws;
            this.auth = auth;
            this.scheduler = scheduler;
            ws.Opened += OnOpened;
            ws.MessageReceived += OnMessage;
            ws.Closed += OnClosed;
        }

        public void Start()
        {
            wantConnected = true;
            if (State == ConnectionState.Disconnected) ConnectNow();
        }

        public void Stop()
        {
            wantConnected = false;
            CancelTimers();
            if (State != ConnectionState.Disconnected) ws.Close();
            SetState(ConnectionState.Disconnected);
        }

        public bool Send(string json)
        {
            if (State != ConnectionState.Connected || !ws.IsOpen) return false;
            ws.Send(json);
            return true;
        }

        private void ConnectNow()
        {
            if (!auth.HasSession)
            {
                // Hesap yoksa bağlanılamaz; oturum açıldığında Start tekrar çağrılır.
                ScheduleReconnect();
                return;
            }
            SetState(ConnectionState.Connecting);
            ws.Connect(settings.WsUrl);
        }

        private void OnOpened()
        {
            ws.Send(ClientMessages.Auth(auth.AccessToken));
            authTimeout?.Dispose();
            authTimeout = scheduler.After(settings.WsAuthTimeoutSeconds, () =>
            {
                if (State == ConnectionState.Connecting) ws.Close();
            });
        }

        private void OnMessage(string raw)
        {
            ServerMessage message = ServerMessage.Parse(raw);
            if (message == null) return;

            if (message.Type == ServerTypes.AuthOk)
            {
                authTimeout?.Dispose();
                attempt = 0;
                SetState(ConnectionState.Connected);
                heartbeat?.Dispose();
                heartbeat = scheduler.Every(settings.HeartbeatIntervalSeconds, () => Send(ClientMessages.Heartbeat()));
            }
            else if (State != ConnectionState.Connected)
            {
                return; // auth tamamlanmadan gelen mesajlar yok sayılır
            }
            MessageReceived?.Invoke(message);
        }

        private async void OnClosed(int code)
        {
            CancelTimers();
            SetState(ConnectionState.Disconnected);

            if (code == CloseReplaced)
            {
                wantConnected = false;
                Replaced?.Invoke();
                return;
            }
            if (!wantConnected) return;

            if (code == CloseUnauthorized)
            {
                bool refreshed = await auth.RefreshAsync();
                if (!refreshed) Log.Warn("Realtime", "Token yenilenemedi, yeniden denenecek.");
            }
            ScheduleReconnect(code == CloseFlood);
        }

        private void ScheduleReconnect(bool extraDelay = false)
        {
            if (!wantConnected) return;
            double delay = Math.Min(settings.ReconnectBackoffMaxSeconds, settings.ReconnectBackoffBaseSeconds * Math.Pow(2, attempt));
            if (extraDelay) delay = settings.ReconnectBackoffMaxSeconds;
            attempt++;
            reconnectTimer?.Dispose();
            reconnectTimer = scheduler.After(delay, () =>
            {
                reconnectTimer = null;
                if (wantConnected && State == ConnectionState.Disconnected) ConnectNow();
            });
        }

        private void CancelTimers()
        {
            heartbeat?.Dispose();
            heartbeat = null;
            authTimeout?.Dispose();
            authTimeout = null;
            reconnectTimer?.Dispose();
            reconnectTimer = null;
        }

        private void SetState(ConnectionState state)
        {
            if (State == state) return;
            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
