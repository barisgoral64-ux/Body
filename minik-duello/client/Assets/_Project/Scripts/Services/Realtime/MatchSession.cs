using System;
using System.Collections.Generic;
using MinikDuello.Core;
using MinikDuello.Domain.Net;
using MinikDuello.Services.Api;

namespace MinikDuello.Services.Realtime
{
    public enum MatchPhase
    {
        Idle,
        /// <summary>Davet gönderildi, yanıt bekleniyor.</summary>
        Inviting,
        Lobby,
        Countdown,
        Playing,
        Reconnecting,
        Finished
    }

    /// <summary>
    /// Sunucudaki odanın istemci aynası. KARAR VERMEZ: yalnızca sunucu mesajlarını duruma çevirir ve kullanıcı girdisini iletir.
    /// Bağlantı kopunca "Arkadaşına yeniden bağlanıyoruz…" akışını yönetir (grace süresi içinde room.resume).
    /// </summary>
    public sealed class MatchSession
    {
        private readonly AppSettings settings;
        private readonly RealtimeClient realtime;
        private readonly AuthSession auth;
        private readonly IScheduler scheduler;
        private IDisposable graceTimer;
        private bool answeredCurrent;

        public MatchPhase Phase { get; private set; } = MatchPhase.Idle;
        public string RoomId { get; private set; }
        public string ResumeToken { get; private set; }
        public string Mode { get; private set; }
        public RoundDto CurrentRound { get; private set; }
        public Dictionary<string, int> Scores { get; private set; } = new Dictionary<string, int>();
        public string OpponentId { get; private set; }
        public bool IsCoop => GameModes.IsCoop(Mode);
        public FinishedDto LastResult { get; private set; }

        public event Action<MatchPhase> PhaseChanged;
        public event Action<InviteReceivedDto> InviteReceived;
        public event Action<string> InviteDeclined;
        public event Action<RoomStateDto> RoomEntered;
        public event Action<int> CountdownStarted;
        public event Action<RoundDto> RoundStarted;
        public event Action<AnswerAckDto> AnswerAcknowledged;
        public event Action<RoundResultDto> RoundEnded;
        public event Action<FinishedDto> MatchFinished;
        public event Action OpponentDisconnected;
        public event Action OpponentReconnected;
        public event Action ReconnectFailed;
        public event Action<QuickChatDto> QuickChatReceived;
        public event Action<string> ErrorReceived;

        public MatchSession(AppSettings settings, RealtimeClient realtime, AuthSession auth, IScheduler scheduler)
        {
            this.settings = settings;
            this.realtime = realtime;
            this.auth = auth;
            this.scheduler = scheduler;
            realtime.MessageReceived += OnMessage;
            realtime.StateChanged += OnConnectionState;
        }

        public bool InMatch => Phase == MatchPhase.Lobby || Phase == MatchPhase.Countdown || Phase == MatchPhase.Playing || Phase == MatchPhase.Reconnecting;

        // --- Kullanıcı eylemleri ---

        public bool SendInvite(string friendId, string mode)
        {
            if (Phase != MatchPhase.Idle && Phase != MatchPhase.Finished) return false;
            if (!realtime.Send(ClientMessages.InviteSend(friendId, mode))) return false;
            SetPhase(MatchPhase.Inviting);
            return true;
        }

        public bool RespondToInvite(string inviteId, bool accept) => realtime.Send(ClientMessages.InviteRespond(inviteId, accept));

        public bool Ready() => realtime.Send(ClientMessages.Ready());

        /// <summary>İstemci yalnızca seçimi gönderir; doğruluk, süre ve puanı sunucu belirler.</summary>
        public bool Answer(string choiceId)
        {
            if (Phase != MatchPhase.Playing || CurrentRound == null) return false;
            if (!CurrentRound.Multi && answeredCurrent) return false;
            if (!realtime.Send(ClientMessages.Answer(CurrentRound.RoundId, choiceId))) return false;
            if (!CurrentRound.Multi) answeredCurrent = true;
            return true;
        }

        public bool SendQuickChat(string messageId)
        {
            if (!InMatch || !QuickChats.IsValid(messageId)) return false;
            return realtime.Send(ClientMessages.QuickChat(messageId));
        }

        public void Leave()
        {
            if (InMatch) realtime.Send(ClientMessages.Leave());
            Reset();
        }

        public void Reset()
        {
            graceTimer?.Dispose();
            graceTimer = null;
            RoomId = null;
            ResumeToken = null;
            CurrentRound = null;
            OpponentId = null;
            Scores = new Dictionary<string, int>();
            answeredCurrent = false;
            SetPhase(MatchPhase.Idle);
        }

        // --- Sunucu mesajları ---

        private void OnMessage(ServerMessage m)
        {
            switch (m.Type)
            {
                case ServerTypes.AuthOk:
                    // Yeniden bağlanıldı: sunucu hâlâ odamızı biliyorsa devam ederiz.
                    if (Phase == MatchPhase.Reconnecting && RoomId != null && ResumeToken != null)
                        realtime.Send(ClientMessages.Resume(RoomId, ResumeToken));
                    break;
                case ServerTypes.InviteReceived:
                    InviteReceived?.Invoke(m.As<InviteReceivedDto>());
                    break;
                case ServerTypes.InviteResolved:
                    OnInviteResolved(m.As<InviteResolvedDto>());
                    break;
                case ServerTypes.RoomState:
                    OnRoomState(m.As<RoomStateDto>());
                    break;
                case ServerTypes.RoomCountdown:
                    SetPhase(MatchPhase.Countdown);
                    CountdownStarted?.Invoke(m.As<CountdownDto>().Seconds);
                    break;
                case ServerTypes.RoomRound:
                    OnRound(m.As<RoundDto>());
                    break;
                case ServerTypes.RoomAnswerAck:
                    AnswerAcknowledged?.Invoke(m.As<AnswerAckDto>());
                    break;
                case ServerTypes.RoomRoundResult:
                    OnRoundResult(m.As<RoundResultDto>());
                    break;
                case ServerTypes.RoomFinished:
                    OnFinished(m.As<FinishedDto>());
                    break;
                case ServerTypes.OpponentDisconnected:
                    OpponentDisconnected?.Invoke();
                    break;
                case ServerTypes.OpponentReconnected:
                    OpponentReconnected?.Invoke();
                    break;
                case ServerTypes.RoomQuickChat:
                    QuickChatReceived?.Invoke(m.As<QuickChatDto>());
                    break;
                case ServerTypes.Error:
                    OnError(m.As<ErrorPayloadDto>().Code);
                    break;
            }
        }

        private void OnInviteResolved(InviteResolvedDto dto)
        {
            if (dto.Status == "declined")
            {
                if (Phase == MatchPhase.Inviting) SetPhase(MatchPhase.Idle);
                InviteDeclined?.Invoke(dto.InviteId);
            }
            // "sent": gönderim onayı, "accepted": room.state zaten gelecek.
        }

        private void OnRoomState(RoomStateDto state)
        {
            RoomId = state.RoomId;
            Mode = state.Mode;
            if (!string.IsNullOrEmpty(state.ResumeToken)) ResumeToken = state.ResumeToken;
            Scores = state.Scores ?? new Dictionary<string, int>();
            OpponentId = null;
            foreach (string p in state.Players)
            {
                if (p != auth.PlayerId) OpponentId = p;
            }

            bool resumed = Phase == MatchPhase.Reconnecting;
            switch (state.State)
            {
                case "waiting": SetPhase(MatchPhase.Lobby); break;
                case "ready": SetPhase(MatchPhase.Countdown); break;
                case "playing": SetPhase(MatchPhase.Playing); break;
                case "reconnecting": SetPhase(MatchPhase.Reconnecting); break;
                case "finished": SetPhase(MatchPhase.Finished); break;
            }
            if (resumed && Phase == MatchPhase.Playing)
            {
                graceTimer?.Dispose();
                graceTimer = null;
            }
            RoomEntered?.Invoke(state);
        }

        private void OnRound(RoundDto round)
        {
            CurrentRound = round;
            answeredCurrent = false;
            SetPhase(MatchPhase.Playing);
            RoundStarted?.Invoke(round);
        }

        private void OnRoundResult(RoundResultDto result)
        {
            if (result.Scores != null) Scores = result.Scores;
            answeredCurrent = true; // tur kapandı
            RoundEnded?.Invoke(result);
        }

        private void OnFinished(FinishedDto result)
        {
            graceTimer?.Dispose();
            graceTimer = null;
            LastResult = result;
            SetPhase(MatchPhase.Finished);
            MatchFinished?.Invoke(result);
        }

        private void OnError(string code)
        {
            // Devam anahtarı reddedilirse / oda yoksa yeniden bağlanma başarısızdır.
            if (Phase == MatchPhase.Reconnecting && (code == "FORBIDDEN" || code == "INVALID_STATE")) FailReconnect();
            else ErrorReceived?.Invoke(code);
        }

        // --- Bağlantı kopması ---

        private void OnConnectionState(ConnectionState state)
        {
            if (state == ConnectionState.Disconnected && (Phase == MatchPhase.Lobby || Phase == MatchPhase.Countdown || Phase == MatchPhase.Playing))
            {
                SetPhase(MatchPhase.Reconnecting);
                graceTimer?.Dispose();
                graceTimer = scheduler.After(settings.ReconnectGraceSeconds, FailReconnect);
            }
            else if (state == ConnectionState.Disconnected && Phase == MatchPhase.Inviting)
            {
                SetPhase(MatchPhase.Idle);
            }
        }

        private void FailReconnect()
        {
            graceTimer?.Dispose();
            graceTimer = null;
            if (Phase != MatchPhase.Reconnecting) return;
            // Maç güvenle sonlandırılır; çocuk cezalandırılmaz (sunucu asgari ödülü verir, bağlanınca profil yenilenir).
            LastResult = new FinishedDto { Reason = "connectionLost" };
            SetPhase(MatchPhase.Finished);
            ReconnectFailed?.Invoke();
        }

        private void SetPhase(MatchPhase phase)
        {
            if (Phase == phase) return;
            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }
    }
}
