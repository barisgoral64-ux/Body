using System.Linq;
using MinikDuello.Core;
using MinikDuello.Domain.Net;
using MinikDuello.Services;
using MinikDuello.Services.Api;
using MinikDuello.Services.Realtime;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MinikDuello.Tests
{
    public sealed class RealtimeTests
    {
        private FakeHttp http;
        private FakeStore store;
        private FakeScheduler scheduler;
        private FakeWebSocket ws;
        private AppSettings settings;
        private AuthSession auth;
        private RealtimeClient rt;
        private MatchSession match;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            http = new FakeHttp();
            store = new FakeStore();
            store.Set("auth.access", "TOKEN");
            store.Set("auth.refresh", "REFRESH");
            store.Set("auth.player", "me");
            scheduler = new FakeScheduler();
            ws = new FakeWebSocket();
            settings = new AppSettings
            {
                WsUrl = "ws://x/ws", ApiUrl = "http://api", HeartbeatIntervalSeconds = 15, WsAuthTimeoutSeconds = 5,
                ReconnectBackoffBaseSeconds = 1, ReconnectBackoffMaxSeconds = 8, ReconnectGraceSeconds = 15
            };
            auth = new AuthSession(settings, http, store);
            rt = new RealtimeClient(settings, ws, auth, scheduler);
            match = new MatchSession(settings, rt, auth, scheduler);
        }

        private void ConnectAndAuth()
        {
            rt.Start();
            ws.SimulateOpen();
            ws.SimulateMessage("{\"v\":1,\"type\":\"auth.ok\",\"payload\":{\"playerId\":\"me\"}}");
        }

        private static JObject Last(FakeWebSocket w) => JObject.Parse(w.Sent.Last());

        // --- RealtimeClient ---

        [Test]
        public void Open_SendsAuthFirst_AndConnectedOnlyAfterAuthOk()
        {
            rt.Start();
            Assert.AreEqual(ConnectionState.Connecting, rt.State);
            ws.SimulateOpen();
            Assert.AreEqual("auth", (string)Last(ws)["type"]);
            Assert.AreEqual("TOKEN", (string)Last(ws)["payload"]["token"]);
            Assert.IsFalse(rt.IsConnected);
            Assert.IsFalse(rt.Send(ClientMessages.Ready())); // auth bitmeden gönderim yok

            ws.SimulateMessage("{\"v\":1,\"type\":\"auth.ok\",\"payload\":{}}");
            Assert.IsTrue(rt.IsConnected);
            Assert.IsTrue(rt.Send(ClientMessages.Ready()));
        }

        [Test]
        public void MessagesBeforeAuth_AreIgnored()
        {
            int received = 0;
            rt.MessageReceived += _ => received++;
            rt.Start();
            ws.SimulateOpen();
            ws.SimulateMessage("{\"v\":1,\"type\":\"room.state\",\"payload\":{}}");
            Assert.AreEqual(0, received);
        }

        [Test]
        public void Heartbeat_IsSentEveryInterval_AndStopsOnDisconnect()
        {
            ConnectAndAuth();
            ws.Sent.Clear();
            scheduler.Advance(15);
            Assert.AreEqual(1, ws.Sent.Count(s => s.Contains("presence.heartbeat")));
            scheduler.Advance(30);
            Assert.AreEqual(3, ws.Sent.Count(s => s.Contains("presence.heartbeat")));

            ws.SimulateClose(1006);
            ws.Sent.Clear();
            scheduler.Advance(0.5);
            scheduler.Advance(60);
            Assert.AreEqual(0, ws.Sent.Count(s => s.Contains("presence.heartbeat")));
        }

        [Test]
        public void Reconnect_UsesExponentialBackoff_CappedAtMax_AndResetsAfterAuthOk()
        {
            rt.Start();
            Assert.AreEqual(1, ws.ConnectCalls);

            double[] waits = { 1, 2, 4, 8, 8 };
            int expectedCalls = 1;
            foreach (double wait in waits)
            {
                ws.SimulateClose(1006);
                scheduler.Advance(wait - 0.01);
                Assert.AreEqual(expectedCalls, ws.ConnectCalls, "erken bağlanmamalı (" + wait + ")");
                scheduler.Advance(0.02);
                expectedCalls++;
                Assert.AreEqual(expectedCalls, ws.ConnectCalls, "zamanında bağlanmalı (" + wait + ")");
            }

            ws.SimulateOpen();
            ws.SimulateMessage("{\"v\":1,\"type\":\"auth.ok\",\"payload\":{}}");
            ws.SimulateClose(1006);
            scheduler.Advance(1.01); // sayaç sıfırlandı → tekrar 1 sn
            Assert.AreEqual(expectedCalls + 1, ws.ConnectCalls);
        }

        [Test]
        public void Replaced_StopsAutoReconnect()
        {
            ConnectAndAuth();
            bool replaced = false;
            rt.Replaced += () => replaced = true;
            ws.SimulateClose(RealtimeClient.CloseReplaced);
            scheduler.Advance(100);
            Assert.IsTrue(replaced);
            Assert.AreEqual(1, ws.ConnectCalls);
        }

        [Test]
        public void Unauthorized_RefreshesTokenThenReconnects()
        {
            http.Handler = spec => spec.Url.EndsWith("/v1/auth/refresh") ? FakeHttp.Ok("{\"accessToken\":\"NEW\",\"refreshToken\":\"R2\"}") : FakeHttp.Status(404);
            ConnectAndAuth();
            ws.SimulateClose(RealtimeClient.CloseUnauthorized);
            scheduler.Advance(1.01);
            Assert.AreEqual(2, ws.ConnectCalls);
            ws.SimulateOpen();
            Assert.AreEqual("NEW", (string)Last(ws)["payload"]["token"]);
        }

        [Test]
        public void AuthTimeout_ClosesStalledConnection()
        {
            rt.Start();
            ws.SimulateOpen();
            scheduler.Advance(5.01);
            Assert.AreEqual(1, ws.CloseCalls);
        }

        [Test]
        public void Stop_PreventsReconnect()
        {
            ConnectAndAuth();
            rt.Stop();
            scheduler.Advance(100);
            Assert.AreEqual(1, ws.ConnectCalls);
            Assert.AreEqual(ConnectionState.Disconnected, rt.State);
        }

        [Test]
        public void NoSession_DoesNotConnect_ButKeepsTrying()
        {
            store.Remove("auth.access");
            rt.Start();
            Assert.AreEqual(0, ws.ConnectCalls);
            store.Set("auth.access", "LATE");
            scheduler.Advance(1.01);
            Assert.AreEqual(1, ws.ConnectCalls);
        }

        // --- MatchSession ---

        private void Server(string type, string payload) =>
            ws.SimulateMessage("{\"v\":1,\"type\":\"" + type + "\",\"payload\":" + payload + "}");

        private void EnterRoom(string state = "waiting")
        {
            Server("room.state", "{\"roomId\":\"r1\",\"mode\":\"mixedMatch\",\"state\":\"" + state + "\",\"players\":[\"me\",\"op\"],\"currentRound\":-1,\"totalRounds\":5,\"scores\":{\"me\":0,\"op\":0},\"opponentConnected\":true,\"resumeToken\":\"RT\"}");
        }

        private const string Round1 =
            "{\"roundId\":\"x1\",\"roundIndex\":0,\"totalRounds\":5,\"kind\":\"color\",\"promptKey\":\"findColor\",\"params\":{\"target\":\"red\"},\"choices\":[{\"id\":\"red\",\"glyph\":\"red\"},{\"id\":\"blue\",\"glyph\":\"blue\"}],\"multi\":false,\"showMs\":0,\"durationMs\":15000}";

        [Test]
        public void Invite_Flow_ToRoomAndRound()
        {
            ConnectAndAuth();
            InviteReceivedDto invite = null;
            match.InviteReceived += i => invite = i;
            Server("invite.received", "{\"inviteId\":\"inv1\",\"from\":{\"playerId\":\"op\",\"username\":\"MutluPanda27\",\"avatarCharacter\":\"panda\"},\"mode\":\"colorRace\",\"expiresAt\":\"2026-01-01T00:00:30Z\"}");
            Assert.AreEqual("MutluPanda27", invite.From.Username);

            Assert.IsTrue(match.RespondToInvite("inv1", true));
            Assert.AreEqual("invite.respond", (string)Last(ws)["type"]);

            EnterRoom();
            Assert.AreEqual(MatchPhase.Lobby, match.Phase);
            Assert.AreEqual("op", match.OpponentId);

            Assert.IsTrue(match.Ready());
            Server("room.countdown", "{\"seconds\":3}");
            Assert.AreEqual(MatchPhase.Countdown, match.Phase);

            Server("room.round", Round1);
            Assert.AreEqual(MatchPhase.Playing, match.Phase);
            Assert.AreEqual("red", match.CurrentRound.Param("target"));
        }

        [Test]
        public void Answer_IsSentOncePerRound_ForSingleChoice()
        {
            ConnectAndAuth();
            EnterRoom();
            Server("room.round", Round1);

            Assert.IsTrue(match.Answer("red"));
            Assert.AreEqual("x1", (string)Last(ws)["payload"]["roundId"]);
            ws.Sent.Clear();
            Assert.IsFalse(match.Answer("blue"));
            Assert.AreEqual(0, ws.Sent.Count);

            Server("room.round", Round1.Replace("x1", "x2"));
            Assert.IsTrue(match.Answer("blue")); // yeni tur
        }

        [Test]
        public void Answer_MultiRound_AllowsManyTaps()
        {
            ConnectAndAuth();
            EnterRoom();
            Server("room.round", Round1.Replace("\"multi\":false", "\"multi\":true"));
            Assert.IsTrue(match.Answer("red"));
            Assert.IsTrue(match.Answer("blue"));
        }

        [Test]
        public void Answer_WithoutRound_OrOutsidePlaying_IsRefused()
        {
            ConnectAndAuth();
            Assert.IsFalse(match.Answer("red"));
            EnterRoom();
            Assert.IsFalse(match.Answer("red"));
        }

        [Test]
        public void QuickChat_OnlyAllowsWhitelist_AndOnlyInMatch()
        {
            ConnectAndAuth();
            Assert.IsFalse(match.SendQuickChat("hello")); // maçta değil
            EnterRoom();
            Assert.IsTrue(match.SendQuickChat("hello"));
            Assert.IsFalse(match.SendQuickChat("merhaba benim adım Ali"));
        }

        [Test]
        public void Finished_ExposesOutcomeAndRewards()
        {
            ConnectAndAuth();
            EnterRoom();
            Server("room.round", Round1);
            FinishedDto got = null;
            match.MatchFinished += f => got = f;
            Server("room.finished", "{\"matchId\":\"m1\",\"reason\":\"completed\",\"outcomes\":[{\"playerId\":\"me\",\"score\":420,\"isWinner\":true,\"starsAwarded\":3,\"coinsAwarded\":30},{\"playerId\":\"op\",\"score\":300,\"isWinner\":false,\"starsAwarded\":2,\"coinsAwarded\":20}],\"coop\":null,\"newRewards\":[\"hat_party\"]}");
            Assert.AreEqual(MatchPhase.Finished, match.Phase);
            Assert.AreEqual(3, got.Outcomes[0].StarsAwarded);
            CollectionAssert.AreEqual(new[] { "hat_party" }, got.NewRewards);
        }

        [Test]
        public void DeclinedInvite_ReturnsToIdle_Softly()
        {
            ConnectAndAuth();
            Assert.IsTrue(match.SendInvite("op", GameModes.ColorRace));
            Assert.AreEqual(MatchPhase.Inviting, match.Phase);
            string declined = null;
            match.InviteDeclined += id => declined = id;
            Server("invite.resolved", "{\"inviteId\":\"i9\",\"status\":\"declined\"}");
            Assert.AreEqual(MatchPhase.Idle, match.Phase);
            Assert.AreEqual("i9", declined);
        }

        [Test]
        public void Disconnect_DuringMatch_ShowsReconnecting_ThenResumesWithToken()
        {
            ConnectAndAuth();
            EnterRoom("playing");
            Server("room.round", Round1);
            Assert.AreEqual(MatchPhase.Playing, match.Phase);

            ws.SimulateClose(1006);
            Assert.AreEqual(MatchPhase.Reconnecting, match.Phase);

            scheduler.Advance(1.01); // yeniden bağlanma denemesi
            ws.SimulateOpen();
            ws.SimulateMessage("{\"v\":1,\"type\":\"auth.ok\",\"payload\":{\"playerId\":\"me\"}}");

            JObject resume = Last(ws);
            Assert.AreEqual("room.resume", (string)resume["type"]);
            Assert.AreEqual("r1", (string)resume["payload"]["roomId"]);
            Assert.AreEqual("RT", (string)resume["payload"]["resumeToken"]);

            EnterRoom("playing");
            Assert.AreEqual(MatchPhase.Playing, match.Phase);
            // Grace zamanlayıcısı iptal edildi: süre dolsa da maç bitmez.
            bool failed = false;
            match.ReconnectFailed += () => failed = true;
            scheduler.Advance(60);
            Assert.IsFalse(failed);
        }

        [Test]
        public void Disconnect_BeyondGrace_EndsMatchSafely()
        {
            ConnectAndAuth();
            EnterRoom("playing");
            bool failed = false;
            match.ReconnectFailed += () => failed = true;

            ws.SimulateClose(1006);
            scheduler.Advance(14);
            Assert.AreEqual(MatchPhase.Reconnecting, match.Phase);
            scheduler.Advance(1.5);
            Assert.IsTrue(failed);
            Assert.AreEqual(MatchPhase.Finished, match.Phase);
        }

        [Test]
        public void ResumeRejected_FailsReconnectImmediately()
        {
            ConnectAndAuth();
            EnterRoom("playing");
            ws.SimulateClose(1006);
            scheduler.Advance(1.01);
            ws.SimulateOpen();
            ws.SimulateMessage("{\"v\":1,\"type\":\"auth.ok\",\"payload\":{}}");

            bool failed = false;
            match.ReconnectFailed += () => failed = true;
            Server("error", "{\"code\":\"INVALID_STATE\"}");
            Assert.IsTrue(failed);
            Assert.AreEqual(MatchPhase.Finished, match.Phase);
        }

        [Test]
        public void Leave_SendsLeave_AndResets()
        {
            ConnectAndAuth();
            EnterRoom("playing");
            match.Leave();
            Assert.AreEqual("room.leave", (string)JObject.Parse(ws.Sent[ws.Sent.Count - 1])["type"]);
            Assert.AreEqual(MatchPhase.Idle, match.Phase);
            Assert.IsNull(match.RoomId);
        }

        [Test]
        public void CoopMode_IsDetected()
        {
            ConnectAndAuth();
            Server("room.state", "{\"roomId\":\"r1\",\"mode\":\"coopStars\",\"state\":\"waiting\",\"players\":[\"me\",\"op\"],\"scores\":{},\"resumeToken\":\"RT\"}");
            Assert.IsTrue(match.IsCoop);
        }

        // --- Protokol ---

        [Test]
        public void ServerMessage_Parse_RejectsGarbage()
        {
            Assert.IsNull(ServerMessage.Parse("bozuk"));
            Assert.IsNull(ServerMessage.Parse("{\"v\":1}"));
            Assert.IsNotNull(ServerMessage.Parse("{\"v\":1,\"type\":\"x\"}"));
        }

        [Test]
        public void ClientMessages_HaveExpectedShape()
        {
            JObject answer = JObject.Parse(ClientMessages.Answer("r", "c"));
            Assert.AreEqual(1, (int)answer["v"]);
            Assert.AreEqual("room.answer", (string)answer["type"]);
            CollectionAssert.AreEquivalent(new[] { "roundId", "choiceId" }, ((JObject)answer["payload"]).Properties().Select(p => p.Name).ToList());
            Assert.IsNull(JObject.Parse(ClientMessages.Ready())["payload"]);
        }
    }
}
