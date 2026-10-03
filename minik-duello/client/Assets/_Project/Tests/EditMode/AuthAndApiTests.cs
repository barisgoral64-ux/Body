using System.Linq;
using System.Threading.Tasks;
using MinikDuello.Core;
using MinikDuello.Domain.Net;
using MinikDuello.Services;
using MinikDuello.Services.Api;
using NUnit.Framework;

namespace MinikDuello.Tests
{
    public sealed class AuthAndApiTests
    {
        private const string RegisterJson =
            "{\"tokens\":{\"accessToken\":\"A1\",\"refreshToken\":\"R1\"},\"isNew\":true,\"player\":{\"playerId\":\"p1\",\"username\":\"MutluPanda27\",\"friendCode\":\"PANDA-4832\"}}";

        private FakeHttp http;
        private FakeStore store;
        private FakeScheduler scheduler;
        private AppSettings settings;
        private AuthSession auth;
        private ApiClient api;

        [SetUp]
        public void SetUp()
        {
            http = new FakeHttp();
            store = new FakeStore();
            scheduler = new FakeScheduler();
            settings = new AppSettings { ApiUrl = "http://api", HttpMaxRetries = 2, HttpRetryBaseSeconds = 0.5f };
            auth = new AuthSession(settings, http, store);
            api = new ApiClient(settings, http, auth, scheduler);
        }

        [Test]
        public async Task Register_StoresTokensAndIdentity_WithoutPersonalData()
        {
            http.Handler = spec => FakeHttp.Ok(RegisterJson);
            Result<bool> result = await auth.EnsureRegisteredAsync();

            Assert.IsTrue(result.IsOk);
            Assert.AreEqual("A1", auth.AccessToken);
            Assert.AreEqual("PANDA-4832", auth.FriendCode);
            Assert.AreEqual("MutluPanda27", auth.Username);
            string body = http.Requests[0].Body;
            StringAssert.Contains("deviceId", body);
            StringAssert.DoesNotContain("email", body.ToLowerInvariant());
            Assert.GreaterOrEqual(auth.DeviceId.Length, 16);
        }

        [Test]
        public async Task Register_Offline_FailsGracefully()
        {
            Result<bool> result = await auth.EnsureRegisteredAsync();
            Assert.IsFalse(result.IsOk);
            Assert.AreEqual(ErrorCode.Network, result.Error);
            Assert.IsFalse(auth.HasSession);
        }

        [Test]
        public async Task DeviceId_IsStableAcrossCalls()
        {
            string first = auth.DeviceId;
            Assert.AreEqual(first, new AuthSession(settings, http, store).DeviceId);
            await Task.CompletedTask;
        }

        [Test]
        public async Task Api_SendsBearerToken_AndParsesJson()
        {
            http.Handler = spec => spec.Url.EndsWith("/v1/auth/anonymous")
                ? FakeHttp.Ok(RegisterJson)
                : FakeHttp.Ok("{\"playerId\":\"p1\",\"username\":\"U\",\"totalStars\":7,\"coins\":3,\"level\":2}");
            Result<PlayerDto> result = await api.GetAsync<PlayerDto>("/v1/me");

            Assert.IsTrue(result.IsOk);
            Assert.AreEqual(7, result.Value.TotalStars);
            Assert.AreEqual("A1", http.Requests.Last().BearerToken);
            Assert.AreEqual("http://api/v1/me", http.Requests.Last().Url);
        }

        [Test]
        public async Task Api_On401_RefreshesOnce_AndRetries()
        {
            store.Set("auth.access", "OLD");
            store.Set("auth.refresh", "R1");
            int meCalls = 0;
            http.Handler = spec =>
            {
                if (spec.Url.EndsWith("/v1/auth/refresh")) return FakeHttp.Ok("{\"accessToken\":\"NEW\",\"refreshToken\":\"R2\"}");
                meCalls++;
                return spec.BearerToken == "NEW" ? FakeHttp.Ok("{\"playerId\":\"p\"}") : FakeHttp.Status(401, "{\"code\":\"UNAUTHORIZED\"}");
            };

            Result<PlayerDto> result = await api.GetAsync<PlayerDto>("/v1/me");
            Assert.IsTrue(result.IsOk);
            Assert.AreEqual(2, meCalls);
            Assert.AreEqual("NEW", auth.AccessToken);
        }

        [Test]
        public async Task Api_On401_WithDeadRefresh_ReRegistersSameDevice()
        {
            store.Set("auth.access", "OLD");
            store.Set("auth.refresh", "DEAD");
            http.Handler = spec =>
            {
                if (spec.Url.EndsWith("/v1/auth/refresh")) return FakeHttp.Error(401, "UNAUTHORIZED");
                if (spec.Url.EndsWith("/v1/auth/anonymous")) return FakeHttp.Ok(RegisterJson.Replace("A1", "FRESH"));
                return spec.BearerToken == "FRESH" ? FakeHttp.Ok("{\"playerId\":\"p\"}") : FakeHttp.Error(401, "UNAUTHORIZED");
            };

            Result<PlayerDto> result = await api.GetAsync<PlayerDto>("/v1/me");
            Assert.IsTrue(result.IsOk);
            Assert.AreEqual("FRESH", auth.AccessToken);
        }

        [Test]
        public async Task Api_PersistentUnauthorized_ReturnsErrorAfterOneRefresh()
        {
            store.Set("auth.access", "OLD");
            http.Handler = spec => FakeHttp.Error(401, "UNAUTHORIZED");
            Result<PlayerDto> result = await api.GetAsync<PlayerDto>("/v1/me");
            Assert.IsFalse(result.IsOk);
            Assert.AreEqual(ErrorCode.Unauthorized, result.Error);
        }

        [Test]
        public async Task Api_NetworkFailure_RetriesWithExponentialBackoff_ThenReportsNetwork()
        {
            store.Set("auth.access", "A");
            Result<PlayerDto> result = await api.GetAsync<PlayerDto>("/v1/me");

            Assert.AreEqual(ErrorCode.Network, result.Error);
            Assert.AreEqual(3, http.Requests.Count); // ilk + 2 yeniden deneme
            CollectionAssert.AreEqual(new[] { 0.5, 1.0 }, scheduler.Delays);
        }

        [Test]
        public async Task Api_RecoversWhenNetworkReturns()
        {
            store.Set("auth.access", "A");
            int calls = 0;
            http.Handler = spec => ++calls < 3 ? HttpResponse.Network() : FakeHttp.Ok("{\"playerId\":\"p\"}");
            Assert.IsTrue((await api.GetAsync<PlayerDto>("/v1/me")).IsOk);
        }

        [TestCase(403, "FRIENDS_DISABLED", ErrorCode.FriendsDisabled)]
        [TestCase(429, "RATE_LIMITED", ErrorCode.RateLimited)]
        [TestCase(404, "NOT_FOUND", ErrorCode.NotFound)]
        [TestCase(409, "ALREADY_FRIENDS", ErrorCode.AlreadyFriends)]
        [TestCase(410, "REQUEST_EXPIRED", ErrorCode.RequestExpired)]
        public async Task Api_MapsServerErrorCodes(int status, string code, ErrorCode expected)
        {
            store.Set("auth.access", "A");
            http.Handler = spec => FakeHttp.Error(status, code);
            Result<FriendListDto> result = await api.GetAsync<FriendListDto>("/v1/friends");
            Assert.AreEqual(expected, result.Error);
        }

        [Test]
        public async Task Api_NonJsonErrorBody_DoesNotCrash()
        {
            store.Set("auth.access", "A");
            http.Handler = spec => new HttpResponse { StatusCode = 502, Body = "<html>Bad gateway</html>" };
            Result<PlayerDto> result = await api.GetAsync<PlayerDto>("/v1/me");
            Assert.AreEqual(ErrorCode.Internal, result.Error);
        }

        [Test]
        public async Task Api_UnitResponse_IgnoresBody()
        {
            store.Set("auth.access", "A");
            http.Handler = spec => FakeHttp.Ok("{\"ok\":true}");
            Result<Unit> result = await api.PostAsync<Unit>("/v1/friends/requests", new FriendCodeRequestDto { FriendCode = "PANDA-2222" });
            Assert.IsTrue(result.IsOk);
            StringAssert.Contains("PANDA-2222", http.Requests.Last().Body);
        }

        [Test]
        public async Task SessionLost_IsRaised_WhenNothingWorks()
        {
            store.Set("auth.access", "OLD");
            store.Set("auth.refresh", "DEAD");
            bool lost = false;
            auth.SessionLost += () => lost = true;
            http.Handler = spec => FakeHttp.Error(500, "INTERNAL");
            Assert.IsFalse(await auth.RefreshAsync());
            Assert.IsTrue(lost);
        }
    }
}
