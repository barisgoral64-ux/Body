using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MinikDuello.Domain.Net;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MinikDuello.Tests
{
    /// <summary>
    /// Sunucunun GERÇEK mesajlarından üretilmiş fixture'lar (server/test/fixtures.test.ts) istemci DTO'larına eksiksiz oturmalı.
    /// Sunucu alan adı değiştirirse bu test kırılır: sessiz uyumsuzluk olmaz.
    /// </summary>
    public sealed class ProtocolContractTests
    {
        private static JObject Server() => JObject.Parse(File.ReadAllText(TestPaths.Protocol("server-messages.json")));
        private static JObject Client() => JObject.Parse(File.ReadAllText(TestPaths.Protocol("client-messages.json")));

        [Test]
        public void ClientMessages_MatchServerFixtures()
        {
            JObject f = Client();
            Assert.IsTrue(JToken.DeepEquals(f["auth"], JObject.Parse(ClientMessages.Auth("TOKEN"))));
            Assert.IsTrue(JToken.DeepEquals(f["heartbeat"], JObject.Parse(ClientMessages.Heartbeat())));
            Assert.IsTrue(JToken.DeepEquals(f["inviteSend"], JObject.Parse(ClientMessages.InviteSend("f1", "mixedMatch"))));
            Assert.IsTrue(JToken.DeepEquals(f["inviteRespond"], JObject.Parse(ClientMessages.InviteRespond("i1", true))));
            Assert.IsTrue(JToken.DeepEquals(f["ready"], JObject.Parse(ClientMessages.Ready())));
            Assert.IsTrue(JToken.DeepEquals(f["answer"], JObject.Parse(ClientMessages.Answer("r", "c"))));
            Assert.IsTrue(JToken.DeepEquals(f["quickChat"], JObject.Parse(ClientMessages.QuickChat("hello"))));
            Assert.IsTrue(JToken.DeepEquals(f["leave"], JObject.Parse(ClientMessages.Leave())));
            Assert.IsTrue(JToken.DeepEquals(f["resume"], JObject.Parse(ClientMessages.Resume("room", "tok"))));
        }

        [Test]
        public void QuickChatIds_MatchServerWhitelist()
        {
            // Sunucu şeması yalnızca bu kimlikleri kabul eder (server/src/domain/quickChat.ts).
            CollectionAssert.AreEqual(new[] { "hello", "great", "congrats", "playAgain", "nice", "thanks" }, QuickChats.Ids);
            foreach (string id in QuickChats.Ids) Assert.IsNotEmpty(QuickChats.Label(id));
        }

        [Test]
        public void ServerPayloads_DeserializeIntoDtos_WithEveryFieldMapped()
        {
            JObject s = Server();
            AssertMapped<RoomStateDto>(s, "room.state");
            AssertMapped<CountdownDto>(s, "room.countdown");
            AssertMapped<RoundDto>(s, "room.round");
            AssertMapped<AnswerAckDto>(s, "room.answerAck");
            AssertMapped<RoundResultDto>(s, "room.roundResult");
            AssertMapped<FinishedDto>(s, "room.finished");
            AssertMapped<QuickChatDto>(s, "room.quickChat");
            AssertMapped<InviteReceivedDto>(s, "invite.received");
        }

        [Test]
        public void ServerPayloads_CarryExpectedValues()
        {
            JObject s = Server();
            var state = Json.Deserialize<RoomStateDto>(s["room.state"].ToString());
            Assert.AreEqual(2, state.Players.Count);
            Assert.IsNotEmpty(state.ResumeToken);
            Assert.AreEqual("mixedMatch", state.Mode);

            var round = Json.Deserialize<RoundDto>(s["room.round"].ToString());
            Assert.Greater(round.Choices.Count, 1);
            Assert.Greater(round.DurationMs, 0);
            Assert.IsNotEmpty(round.Param("target"));

            var finished = Json.Deserialize<FinishedDto>(s["room.finished"].ToString());
            Assert.AreEqual(2, finished.Outcomes.Count);
            Assert.AreEqual("completed", finished.Reason);
            Assert.IsTrue(finished.Outcomes.All(o => o.StarsAwarded > 0 && o.CoinsAwarded > 0), "Herkes ödül almalı");
            Assert.IsTrue(finished.Outcomes.Any(o => o.IsWinner));

            var result = Json.Deserialize<RoundResultDto>(s["room.roundResult"].ToString());
            Assert.IsNotEmpty(result.CorrectChoiceIds);
            Assert.AreEqual(2, result.Scores.Count);

            var invite = Json.Deserialize<InviteReceivedDto>(s["invite.received"].ToString());
            Assert.IsNotEmpty(invite.From.Username);
            Assert.IsNull(((JObject)s["invite.received"]["from"])["friendCode"], "Davet eden kişinin arkadaş kodu sızmamalı");
        }

        [Test]
        public void StarsRound_IsMultiChoice()
        {
            JObject s = Server();
            if (s["room.round.stars"] == null) Assert.Ignore("Bu fixture'da yıldız turu yok.");
            Assert.IsTrue(Json.Deserialize<RoundDto>(s["room.round.stars"].ToString()).Multi);
        }

        private static void AssertMapped<T>(JObject fixtures, string key)
        {
            Assert.IsNotNull(fixtures[key], key + " fixture'ı yok");
            var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name.ToLowerInvariant()).ToList();
            foreach (JProperty p in ((JObject)fixtures[key]).Properties())
            {
                CollectionAssert.Contains(props, p.Name.ToLowerInvariant(), typeof(T).Name + " DTO'sunda '" + p.Name + "' alanı eksik");
            }
            Assert.DoesNotThrow(() => Json.Deserialize<T>(fixtures[key].ToString()));
        }
    }
}
