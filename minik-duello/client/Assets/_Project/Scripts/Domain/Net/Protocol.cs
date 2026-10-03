using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MinikDuello.Domain.Net
{
    public static class GameModes
    {
        public const string ColorRace = "colorRace";
        public const string ShapeRace = "shapeRace";
        public const string NumberRace = "numberRace";
        public const string MemoryDuel = "memoryDuel";
        public const string PuzzleRace = "puzzleRace";
        public const string StarCollect = "starCollect";
        public const string CoopStars = "coopStars";
        public const string CoopPuzzle = "coopPuzzle";
        public const string MixedMatch = "mixedMatch";

        public static readonly string[] Competitive = { ColorRace, ShapeRace, NumberRace, MemoryDuel, PuzzleRace, StarCollect, MixedMatch };
        public static readonly string[] Cooperative = { CoopStars, CoopPuzzle };

        public static bool IsCoop(string mode) => Array.IndexOf(Cooperative, mode) >= 0;
    }

    /// <summary>Sunucu → istemci mesaj türleri (server/src/protocol.ts ile aynı).</summary>
    public static class ServerTypes
    {
        public const string AuthOk = "auth.ok";
        public const string RoomState = "room.state";
        public const string RoomReady = "room.ready";
        public const string RoomCountdown = "room.countdown";
        public const string RoomRound = "room.round";
        public const string RoomAnswerAck = "room.answerAck";
        public const string RoomRoundResult = "room.roundResult";
        public const string RoomFinished = "room.finished";
        public const string OpponentDisconnected = "room.opponentDisconnected";
        public const string OpponentReconnected = "room.opponentReconnected";
        public const string RoomQuickChat = "room.quickChat";
        public const string InviteReceived = "invite.received";
        public const string InviteResolved = "invite.resolved";
        public const string PresenceUpdate = "presence.update";
        public const string FriendEvent = "friend.event";
        public const string Error = "error";
    }

    public sealed class ServerMessage
    {
        public int V;
        public string Type;
        public JObject Payload;

        public static ServerMessage Parse(string json)
        {
            try
            {
                JObject root = JObject.Parse(json);
                string type = (string)root["type"];
                if (string.IsNullOrEmpty(type)) return null;
                return new ServerMessage
                {
                    V = root["v"] != null ? (int)root["v"] : 0,
                    Type = type,
                    Payload = root["payload"] as JObject ?? new JObject()
                };
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public T As<T>() => Json.Deserialize<T>(Payload.ToString(Formatting.None));
    }

    /// <summary>İstemci → sunucu mesajları. İstemci yalnızca girdi gönderir; sonuçları sunucu belirler.</summary>
    public static class ClientMessages
    {
        public const int ProtocolVersion = 1;

        public static string Auth(string token) => Build("auth", new JObject { ["token"] = token });
        public static string Heartbeat() => Build("presence.heartbeat", null);
        public static string InviteSend(string receiverId, string mode) => Build("invite.send", new JObject { ["receiverId"] = receiverId, ["mode"] = mode });
        public static string InviteRespond(string inviteId, bool accept) => Build("invite.respond", new JObject { ["inviteId"] = inviteId, ["accept"] = accept });
        public static string Ready() => Build("room.ready", null);
        public static string Answer(string roundId, string choiceId) => Build("room.answer", new JObject { ["roundId"] = roundId, ["choiceId"] = choiceId });
        public static string QuickChat(string messageId) => Build("room.quickChat", new JObject { ["message"] = messageId });
        public static string Leave() => Build("room.leave", null);
        public static string Resume(string roomId, string resumeToken) => Build("room.resume", new JObject { ["roomId"] = roomId, ["resumeToken"] = resumeToken });

        private static string Build(string type, JObject payload)
        {
            var root = new JObject { ["v"] = ProtocolVersion, ["type"] = type };
            if (payload != null) root["payload"] = payload;
            return root.ToString(Formatting.None);
        }
    }

    /// <summary>Hazır mesajlar: serbest metin yok, yalnızca bu kapalı küme gönderilebilir.</summary>
    public static class QuickChats
    {
        public static readonly string[] Ids = { "hello", "great", "congrats", "playAgain", "nice", "thanks" };

        public static string Label(string id)
        {
            switch (id)
            {
                case "hello": return "Merhaba!";
                case "great": return "Harika!";
                case "congrats": return "Tebrikler!";
                case "playAgain": return "Tekrar oynayalım!";
                case "nice": return "Çok güzel!";
                case "thanks": return "Teşekkürler!";
                default: return string.Empty;
            }
        }

        public static bool IsValid(string id) => Array.IndexOf(Ids, id) >= 0;
    }

    // --- Yük (payload) DTO'ları ---

    public sealed class RoomStateDto
    {
        public string RoomId { get; set; }
        public string Mode { get; set; }
        public string State { get; set; }
        public List<string> Players { get; set; } = new List<string>();
        public int CurrentRound { get; set; }
        public int TotalRounds { get; set; }
        public Dictionary<string, int> Scores { get; set; } = new Dictionary<string, int>();
        public bool OpponentConnected { get; set; }
        public string ResumeToken { get; set; }
    }

    public sealed class RoundChoiceDto
    {
        public string Id { get; set; }
        public string Glyph { get; set; }
    }

    public sealed class RoundDto
    {
        public string RoundId { get; set; }
        public int RoundIndex { get; set; }
        public int TotalRounds { get; set; }
        public string Kind { get; set; }
        public string PromptKey { get; set; }
        public Dictionary<string, object> Params { get; set; } = new Dictionary<string, object>();
        public List<RoundChoiceDto> Choices { get; set; } = new List<RoundChoiceDto>();
        public bool Multi { get; set; }
        public int ShowMs { get; set; }
        public int DurationMs { get; set; }
        public bool Resumed { get; set; }

        public string Param(string key) => Params.TryGetValue(key, out object v) && v != null ? Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
    }

    public sealed class AnswerAckDto
    {
        public string RoundId { get; set; }
        public bool Correct { get; set; }
        public bool Taken { get; set; }
        public int Points { get; set; }
        public int Total { get; set; }
    }

    public sealed class TeamProgressDto
    {
        public int Progress { get; set; }
        public int Target { get; set; }
    }

    public sealed class RoundResultDto
    {
        public string RoundId { get; set; }
        public List<string> CorrectChoiceIds { get; set; } = new List<string>();
        public Dictionary<string, string> ClaimedBy { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, int> RoundPoints { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> Scores { get; set; } = new Dictionary<string, int>();
        public TeamProgressDto Team { get; set; }
    }

    public sealed class OutcomeDto
    {
        public string PlayerId { get; set; }
        public int Score { get; set; }
        public bool IsWinner { get; set; }
        public int StarsAwarded { get; set; }
        public int CoinsAwarded { get; set; }
    }

    public sealed class CoopResultDto
    {
        public bool Success { get; set; }
        public int Progress { get; set; }
        public int Target { get; set; }
    }

    public sealed class FinishedDto
    {
        public string MatchId { get; set; }
        /// <summary>completed | disconnect | timeout | declined</summary>
        public string Reason { get; set; }
        public List<OutcomeDto> Outcomes { get; set; } = new List<OutcomeDto>();
        public CoopResultDto Coop { get; set; }
        public List<string> NewRewards { get; set; } = new List<string>();
    }

    public sealed class InviteReceivedDto
    {
        public string InviteId { get; set; }
        public PublicPlayerDto From { get; set; }
        public string Mode { get; set; }
        public string ExpiresAt { get; set; }
    }

    public sealed class InviteResolvedDto
    {
        public string InviteId { get; set; }
        /// <summary>sent | accepted | declined</summary>
        public string Status { get; set; }
        public string RoomId { get; set; }
    }

    public sealed class PresenceUpdateDto
    {
        public string PlayerId { get; set; }
        public string Status { get; set; }
    }

    public sealed class QuickChatDto
    {
        public string From { get; set; }
        public string Message { get; set; }
    }

    public sealed class CountdownDto
    {
        public int Seconds { get; set; }
    }

    public sealed class ErrorPayloadDto
    {
        public string Code { get; set; }
    }

    public sealed class AuthOkDto
    {
        public string PlayerId { get; set; }
        public string RoomId { get; set; }
    }

    public sealed class FriendEventDto
    {
        public string Type { get; set; }
        public string From { get; set; }
    }
}
