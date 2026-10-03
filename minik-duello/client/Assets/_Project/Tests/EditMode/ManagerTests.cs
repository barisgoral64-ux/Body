using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MinikDuello.Core;
using MinikDuello.Domain.Levels;
using MinikDuello.Domain.Net;
using MinikDuello.Services;
using MinikDuello.Services.Api;
using MinikDuello.Services.Managers;
using MinikDuello.Services.Save;
using NUnit.Framework;

namespace MinikDuello.Tests
{
    public sealed class ManagerTests
    {
        private FakeHttp http;
        private FakeStore store;
        private FakeScheduler scheduler;
        private FakeClock clock;
        private AppSettings settings;
        private AuthSession auth;
        private ApiClient api;
        private SaveManager save;
        private LevelManager levels;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            http = new FakeHttp();
            store = new FakeStore();
            store.Set("auth.access", "A");
            scheduler = new FakeScheduler();
            clock = new FakeClock();
            settings = new AppSettings { ApiUrl = "http://api", HttpMaxRetries = 0 };
            auth = new AuthSession(settings, http, store);
            api = new ApiClient(settings, http, auth, scheduler);
            save = new SaveManager(store);
            var catalog = LevelCatalog.FromJson(File.ReadAllText(TestPaths.LevelsJson()), 100, 10);
            levels = new LevelManager(catalog, save, api, new DifficultyManager());
        }

        private static string ResultJson(int stars) =>
            "{\"stars\":" + stars + ",\"starsGained\":" + stars + ",\"coinsGained\":10,\"newRewards\":[],\"nextLevelId\":2}";

        // --- LevelManager ---

        [Test]
        public void Unlocking_FollowsStars()
        {
            Assert.IsTrue(levels.IsUnlocked(1));
            Assert.IsFalse(levels.IsUnlocked(2));
            Assert.IsFalse(levels.IsUnlocked(0));
            Assert.IsFalse(levels.IsUnlocked(101));
        }

        [Test]
        public async Task Complete_Online_SavesLocally_AndReturnsServerRewards()
        {
            http.Handler = spec => FakeHttp.Ok(ResultJson(3));
            Result<LevelCompletion> r = await levels.CompleteAsync(1, 500, 20000);

            Assert.IsTrue(r.IsOk);
            Assert.AreEqual(3, r.Value.Stars);
            Assert.IsTrue(r.Value.NextUnlocked);
            Assert.AreEqual(2, r.Value.NextLevelId);
            Assert.AreEqual(10, r.Value.Server.CoinsGained);
            Assert.AreEqual(0, levels.PendingCount);
            Assert.IsTrue(levels.IsUnlocked(2));
            StringAssert.Contains("\"levelId\":1", http.Requests.Last().Body);
        }

        [Test]
        public async Task Complete_Offline_WorksAndQueuesForLater()
        {
            Result<LevelCompletion> r = await levels.CompleteAsync(1, 300, 20000);

            Assert.IsTrue(r.IsOk);
            Assert.AreEqual(1, r.Value.Stars);
            Assert.IsNull(r.Value.Server);
            Assert.AreEqual(1, levels.PendingCount);
            Assert.IsTrue(levels.IsUnlocked(2)); // çocuk çevrimdışıyken de ilerler
        }

        [Test]
        public async Task PendingResults_AreFlushedInOrder_WhenOnlineAgain()
        {
            await levels.CompleteAsync(1, 400, 20000);
            await levels.CompleteAsync(2, 400, 20000);
            Assert.AreEqual(2, levels.PendingCount);

            http.Handler = spec => FakeHttp.Ok(ResultJson(1));
            await levels.FlushPendingAsync();

            Assert.AreEqual(0, levels.PendingCount);
            var ids = http.Requests.Where(x => x.Url.Contains("level-result")).Select(x => Json.Deserialize<LevelResultRequestDto>(x.Body).LevelId).ToList();
            CollectionAssert.AreEqual(new[] { 1, 2 }, ids.Skip(ids.Count - 2).ToList());
        }

        [Test]
        public async Task PendingResults_SurviveRestart()
        {
            await levels.CompleteAsync(1, 400, 20000);
            var reloaded = new SaveManager(store);
            Assert.AreEqual(1, reloaded.Data.Pending.Count);
            Assert.AreEqual(1, reloaded.Data.Levels[1].Stars >= 1 ? 1 : 0);
        }

        [Test]
        public async Task PermanentRejection_DropsItem_ButTransientErrorKeepsIt()
        {
            await levels.CompleteAsync(1, 400, 20000);
            http.Handler = spec => FakeHttp.Error(500, "INTERNAL");
            await levels.FlushPendingAsync();
            Assert.AreEqual(1, levels.PendingCount); // geçici hata: tut

            http.Handler = spec => FakeHttp.Error(400, "INVALID_INPUT");
            await levels.FlushPendingAsync();
            Assert.AreEqual(0, levels.PendingCount); // kalıcı red: at
        }

        [Test]
        public async Task Complete_LockedLevel_IsRefused_AndScoreIsClamped()
        {
            Result<LevelCompletion> locked = await levels.CompleteAsync(5, 500, 20000);
            Assert.AreEqual(ErrorCode.Forbidden, locked.Error);

            Result<LevelCompletion> cheat = await levels.CompleteAsync(1, 99999, 20000);
            Assert.IsTrue(cheat.IsOk);
            Assert.AreEqual(500, levels.BestScore(1)); // hedef skoru aşamaz
        }

        [Test]
        public async Task BestResults_NeverDecrease()
        {
            await levels.CompleteAsync(1, 500, 20000);
            await levels.CompleteAsync(1, 100, 20000);
            Assert.AreEqual(3, levels.Stars(1));
            Assert.AreEqual(500, levels.BestScore(1));
        }

        [Test]
        public async Task Struggling_TwiceOnSameLevel_EasesNextAttempt()
        {
            await levels.CompleteAsync(1, 250, 20000); // 1 yıldız
            Assert.IsFalse(levels.PlanFor(1).HintsEnabled);
            await levels.CompleteAsync(1, 250, 20000);
            DifficultyManager.Plan plan = levels.PlanFor(1);
            Assert.IsTrue(plan.HintsEnabled);
            Assert.Less(plan.Level.ObjectCount, levels.Catalog.Get(1).ObjectCount);
            await levels.CompleteAsync(1, 500, 20000); // iyi sonuç seriyi sıfırlar
            Assert.IsFalse(levels.PlanFor(1).HintsEnabled);
        }

        [Test]
        public async Task SyncFromServer_MergesMaxValues()
        {
            await levels.CompleteAsync(1, 250, 20000);
            http.Handler = spec => FakeHttp.Ok("{\"levels\":[{\"levelId\":1,\"stars\":3,\"bestScore\":500,\"unlocked\":true},{\"levelId\":2,\"stars\":2,\"bestScore\":380,\"unlocked\":true}]}");
            Assert.IsTrue(await levels.SyncFromServerAsync());
            Assert.AreEqual(3, levels.Stars(1));
            Assert.AreEqual(2, levels.Stars(2));
            Assert.AreEqual(5, levels.TotalStars());
            Assert.AreEqual(3, levels.NextRecommended());
        }

        // --- SaveManager ---

        [Test]
        public void CorruptSave_IsBackedUp_AndGameContinues()
        {
            store.Set("save.v1", "{bozuk json");
            var fresh = new SaveManager(store);
            Assert.AreEqual(0, fresh.Data.Levels.Count);
            Assert.AreEqual("{bozuk json", store.Get("save.v1.corrupt"));
        }

        [Test]
        public void SaveRoundTrip_AndValueClamping()
        {
            save.Update(d =>
            {
                d.MusicVolume = 40;
                d.Levels[3] = new LevelRecord { Stars = 2, BestScore = 380 };
            });
            store.Set("save.v1", store.Get("save.v1").Replace("\"musicVolume\":40", "\"musicVolume\":9999"));
            var again = new SaveManager(store);
            Assert.AreEqual(100, again.Data.MusicVolume);
            Assert.AreEqual(2, again.Data.Levels[3].Stars);
        }

        // --- PlaytimeLimiter ---

        [Test]
        public void Playtime_LimitsPerDay_AndResetsNextDay()
        {
            var limiter = new PlaytimeLimiter(save, clock);
            Assert.AreEqual(LimitState.Unlimited, limiter.State(0));
            Assert.AreEqual(LimitState.Active, limiter.State(30));

            limiter.AddPlaySeconds(29 * 60);
            Assert.AreEqual(LimitState.Active, limiter.State(30));
            limiter.AddPlaySeconds(60);
            Assert.AreEqual(LimitState.Exceeded, limiter.State(30));
            Assert.AreEqual(0d, limiter.RemainingMinutes(30));

            clock.Now = clock.Now.AddDays(1);
            Assert.AreEqual(LimitState.Active, limiter.State(30));
            Assert.AreEqual(0d, limiter.MinutesToday());
        }

        [Test]
        public void Playtime_IgnoresNegativeAndPrunesOldDays()
        {
            var limiter = new PlaytimeLimiter(save, clock);
            limiter.AddPlaySeconds(-100);
            Assert.AreEqual(0d, limiter.MinutesToday());
            for (int i = 0; i < 30; i++)
            {
                clock.Now = clock.Now.AddDays(1);
                limiter.AddPlaySeconds(60);
            }
            Assert.LessOrEqual(save.Data.PlayMinutes.Count, 14);
        }

        // --- ParentControlManager ---

        [Test]
        public async Task VerifyPin_WithoutPin_NeverSucceeds()
        {
            var parent = new ParentControlManager(api);
            http.Handler = spec => FakeHttp.Ok("{\"hasPin\":false,\"friendsEnabled\":false}");
            Result<ParentSettingsDto> r = await parent.VerifyPinAsync("1234");
            Assert.IsFalse(r.IsOk);
            Assert.AreEqual(ErrorCode.Forbidden, r.Error);
            Assert.IsFalse(http.Requests.Any(x => x.Method == "PUT"));
        }

        [Test]
        public async Task VerifyPin_WrongPin_IsRejectedByServer()
        {
            var parent = new ParentControlManager(api);
            http.Handler = spec => spec.Method == "PUT" ? FakeHttp.Error(403, "FORBIDDEN") : FakeHttp.Ok("{\"hasPin\":true}");
            Result<ParentSettingsDto> r = await parent.VerifyPinAsync("0000");
            Assert.AreEqual(ErrorCode.Forbidden, r.Error);
        }

        [Test]
        public async Task Settings_DefaultToSocialOff_AndUpdateAppliesServerValues()
        {
            var parent = new ParentControlManager(api);
            Assert.IsFalse(parent.SocialAvailable);
            Assert.IsFalse(parent.MultiplayerAvailable);

            http.Handler = spec => FakeHttp.Ok("{\"hasPin\":true,\"friendsEnabled\":true,\"multiplayerEnabled\":true,\"onlineStatusVisible\":false}");
            Result<ParentSettingsDto> r = await parent.UpdateAsync(new { friendsEnabled = true, multiplayerEnabled = true }, "1234");
            Assert.IsTrue(r.IsOk);
            Assert.IsTrue(parent.MultiplayerAvailable);
            StringAssert.Contains("\"pin\":\"1234\"", http.Requests.Last().Body);
        }

        // --- FriendManager ---

        [Test]
        public async Task Friends_Refresh_AndPresenceUpdates()
        {
            var friends = new FriendManager(api);
            http.Handler = spec => FakeHttp.Ok("{\"friends\":[{\"playerId\":\"f1\",\"username\":\"TatliTilki9\",\"avatarCharacter\":\"fox\",\"presence\":\"online\"}]}");
            Assert.IsTrue((await friends.RefreshAsync()).IsOk);
            Assert.AreEqual("online", friends.PresenceOf("f1"));
            Assert.AreEqual("offline", friends.PresenceOf("bilinmeyen"));

            friends.ApplyPresence(new PresenceUpdateDto { PlayerId = "f1", Status = "playing" });
            Assert.AreEqual("playing", friends.PresenceOf("f1"));
            Assert.AreEqual("playing", friends.Friends[0].Presence);
        }

        [Test]
        public async Task Friends_Disabled_ReturnsErrorCode_NotCrash()
        {
            var friends = new FriendManager(api);
            http.Handler = spec => FakeHttp.Error(403, "FRIENDS_DISABLED");
            Result<System.Collections.Generic.IReadOnlyList<FriendDto>> r = await friends.RefreshAsync();
            Assert.AreEqual(ErrorCode.FriendsDisabled, r.Error);
        }

        [Test]
        public async Task SendRequest_TrimsCode()
        {
            var friends = new FriendManager(api);
            http.Handler = spec => FakeHttp.Ok("{\"ok\":true}");
            await friends.SendRequestAsync("  PANDA-4832 ");
            StringAssert.Contains("\"friendCode\":\"PANDA-4832\"", http.Requests.Last().Body);
        }

        // --- PlayerManager / Character ---

        [Test]
        public async Task Player_Refresh_CachesForOfflineDisplay()
        {
            var player = new PlayerManager(api, save, auth);
            http.Handler = spec => FakeHttp.Ok("{\"playerId\":\"p\",\"username\":\"MutluPanda27\",\"friendCode\":\"PANDA-4832\",\"avatarCharacter\":\"fox\",\"totalStars\":12,\"coins\":40,\"level\":3}");
            bool notified = false;
            EventBus.Subscribe<PlayerChanged>(_ => notified = true);
            Assert.IsTrue((await player.RefreshAsync()).IsOk);
            Assert.IsTrue(notified);

            http.Handler = spec => HttpResponse.Network();
            var offline = new PlayerManager(api, new SaveManager(store), auth);
            Assert.AreEqual("MutluPanda27", offline.Username);
            Assert.AreEqual(12, offline.Stars);
            Assert.AreEqual("fox", offline.Character);
        }

        [Test]
        public async Task Rewards_Inventory_FeedsCharacterManager()
        {
            var player = new PlayerManager(api, save, auth);
            var rewards = new RewardManager(api, save, player);
            var characters = new CharacterManager(save);
            http.Handler = spec => FakeHttp.Ok("{\"items\":[{\"rewardId\":\"char_panda\",\"equipped\":false},{\"rewardId\":\"char_cat\",\"equipped\":false},{\"rewardId\":\"hat_party\",\"equipped\":true}]}");
            await rewards.RefreshInventoryAsync();

            CollectionAssert.AreEquivalent(new[] { "panda", "cat" }, characters.OwnedCharacters().ToList());
            Assert.IsFalse(characters.IsCharacterOwned("dog"));
            Assert.AreEqual("hat_party", characters.EquippedBySlot()["hat"]);
            Assert.IsTrue(rewards.Owns("hat_party"));
        }

        // --- Hesap silme ---

        [Test]
        public async Task DeleteAccount_Success_PublishesEvent_AndSendsConfirmedPin()
        {
            var parent = new ParentControlManager(api);
            bool deleted = false;
            EventBus.Subscribe<AccountDeleted>(_ => deleted = true);
            http.Handler = spec => FakeHttp.Ok("{\"ok\":true}");

            Result<Unit> r = await parent.DeleteAccountAsync("1234");
            Assert.IsTrue(r.IsOk);
            Assert.IsTrue(deleted);
            string body = http.Requests.Last().Body;
            StringAssert.Contains("\"confirm\":true", body);
            StringAssert.Contains("\"pin\":\"1234\"", body);
            Assert.IsFalse(parent.SocialAvailable);
        }

        [Test]
        public async Task DeleteAccount_WrongPin_KeepsEverything()
        {
            var parent = new ParentControlManager(api);
            bool deleted = false;
            EventBus.Subscribe<AccountDeleted>(_ => deleted = true);
            http.Handler = spec => FakeHttp.Error(403, "FORBIDDEN");

            Result<Unit> r = await parent.DeleteAccountAsync("0000");
            Assert.AreEqual(ErrorCode.Forbidden, r.Error);
            Assert.IsFalse(deleted);
        }

        [Test]
        public void ClearAll_RemovesIdentity_AndNextRegistrationUsesNewDeviceId()
        {
            string oldDevice = auth.DeviceId;
            store.Set("auth.player", "p1");
            auth.ClearAll();
            Assert.IsFalse(auth.HasSession);
            Assert.IsNull(auth.PlayerId);
            Assert.AreNotEqual(oldDevice, auth.DeviceId);
        }

        [Test]
        public void SaveReset_ClearsProgressAndPendingResults()
        {
            save.Update(d =>
            {
                d.Levels[1] = new LevelRecord { Stars = 3, BestScore = 500 };
                d.Pending.Add(new PendingResult { LevelId = 1, Score = 500, DurationMs = 20000 });
            });
            save.Reset();
            Assert.AreEqual(0, save.Data.Levels.Count);
            Assert.AreEqual(0, save.Data.Pending.Count);
            Assert.AreEqual(0, new SaveManager(store).Data.Levels.Count);
        }
    }
}
