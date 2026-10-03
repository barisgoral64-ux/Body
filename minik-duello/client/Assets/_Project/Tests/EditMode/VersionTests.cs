using System.Linq;
using System.Threading.Tasks;
using MinikDuello.Core;
using MinikDuello.Domain.Net;
using MinikDuello.Services;
using MinikDuello.Services.Api;
using MinikDuello.Services.Managers;
using MinikDuello.Services.Save;
using NUnit.Framework;

namespace MinikDuello.Tests
{
    public sealed class VersionTests
    {
        private FakeHttp http;
        private FakeStore store;
        private AppSettings settings;
        private ConnectivityState connectivity;
        private ApiClient api;
        private SaveManager save;

        [SetUp]
        public void SetUp()
        {
            http = new FakeHttp();
            store = new FakeStore();
            store.Set("auth.access", "A");
            settings = new AppSettings { ApiUrl = "http://api", AppVersion = "1.2.0", PackageName = "com.test.minik", HttpMaxRetries = 0 };
            connectivity = new ConnectivityState();
            api = new ApiClient(settings, http, new AuthSession(settings, http, store), new FakeScheduler(), connectivity);
            save = new SaveManager(store);
        }

        private static AppVersionDto Policy(string min, string latest, string url = "") =>
            new AppVersionDto { MinSupportedVersion = min, LatestVersion = latest, UpdateUrl = url };

        [Test]
        public void Compare_IsNumeric_NotLexical()
        {
            Assert.AreEqual(-1, AppVersion.Compare("1.2.9", "1.10.0"));
            Assert.AreEqual(1, AppVersion.Compare("2.0.0", "1.99.99"));
            Assert.AreEqual(0, AppVersion.Compare("1.0.0-test", "1.0.0"));
            Assert.IsNull(AppVersion.Compare("abc", "1.0.0"));
            Assert.IsNull(AppVersion.Compare("1.0", "1.0.0"));
        }

        [TestCase("1.2.0", "1.0.0", "1.2.0", UpdateStatus.UpToDate)]
        [TestCase("1.2.0", "1.0.0", "1.3.0", UpdateStatus.Available)]
        [TestCase("1.2.0", "1.2.1", "1.3.0", UpdateStatus.Required)]
        [TestCase("1.2.0", "1.2.0", "1.2.0", UpdateStatus.UpToDate)]
        [TestCase("0.1.0-test", "0.1.0", "0.1.0", UpdateStatus.UpToDate)]
        public void Evaluate_FollowsPolicy(string current, string min, string latest, UpdateStatus expected)
        {
            Assert.AreEqual(expected, AppVersion.Evaluate(current, Policy(min, latest)));
        }

        [Test]
        public void Evaluate_BrokenPolicyNeverLocksTheChildOut()
        {
            Assert.AreEqual(UpdateStatus.UpToDate, AppVersion.Evaluate("1.2.0", Policy("bozuk", "yok")));
            Assert.AreEqual(UpdateStatus.UpToDate, AppVersion.Evaluate("bozuk", Policy("1.0.0", "1.0.0")));
            Assert.AreEqual(UpdateStatus.UpToDate, AppVersion.Evaluate("1.2.0", null));
        }

        [Test]
        public async Task Checker_ReportsRequired_Optional_AndUpToDate()
        {
            var checker = new VersionChecker(settings, api, save);

            http.Handler = spec => FakeHttp.Ok("{\"minSupportedVersion\":\"1.3.0\",\"latestVersion\":\"1.4.0\",\"updateUrl\":\"https://store/x\"}");
            Result<UpdateInfo> required = await checker.CheckAsync();
            Assert.AreEqual(UpdateStatus.Required, required.Value.Status);
            Assert.AreEqual("https://store/x", required.Value.StoreUrl);
            Assert.AreEqual("1.4.0", required.Value.LatestVersion);

            http.Handler = spec => FakeHttp.Ok("{\"minSupportedVersion\":\"1.0.0\",\"latestVersion\":\"1.4.0\",\"updateUrl\":\"\"}");
            Result<UpdateInfo> optional = await checker.CheckAsync();
            Assert.AreEqual(UpdateStatus.Available, optional.Value.Status);

            http.Handler = spec => FakeHttp.Ok("{\"minSupportedVersion\":\"1.0.0\",\"latestVersion\":\"1.2.0\",\"updateUrl\":\"\"}");
            Assert.AreEqual(UpdateStatus.UpToDate, (await checker.CheckAsync()).Value.Status);
        }

        [Test]
        public async Task Checker_StoreUrl_FallsBackToPlayStore()
        {
            var checker = new VersionChecker(settings, api, save);
            http.Handler = spec => FakeHttp.Ok("{\"minSupportedVersion\":\"1.0.0\",\"latestVersion\":\"1.9.0\",\"updateUrl\":\"\"}");
            Assert.AreEqual("https://play.google.com/store/apps/details?id=com.test.minik", (await checker.CheckAsync()).Value.StoreUrl);
        }

        [Test]
        public async Task Checker_Offline_NeverForcesUpdate()
        {
            var checker = new VersionChecker(settings, api, save);
            Result<UpdateInfo> r = await checker.CheckAsync();
            Assert.IsFalse(r.IsOk);
            Assert.AreEqual(ErrorCode.Network, r.Error);
            Assert.IsFalse(connectivity.UpdateRequired);
        }

        [Test]
        public async Task Checker_ProtocolMismatch_MeansRequired()
        {
            var checker = new VersionChecker(settings, api, save);
            http.Handler = spec => new HttpResponse { StatusCode = 426, Body = "{\"code\":\"PROTOCOL_MISMATCH\"}" };
            Result<UpdateInfo> r = await checker.CheckAsync();
            Assert.AreEqual(UpdateStatus.Required, r.Value.Status);
            Assert.IsTrue(connectivity.UpdateRequired);
        }

        [Test]
        public async Task OptionalPrompt_IsAskedOncePerVersion()
        {
            var checker = new VersionChecker(settings, api, save);
            http.Handler = spec => FakeHttp.Ok("{\"minSupportedVersion\":\"1.0.0\",\"latestVersion\":\"1.4.0\",\"updateUrl\":\"\"}");
            UpdateInfo info = (await checker.CheckAsync()).Value;
            Assert.IsTrue(checker.ShouldPromptOptional(info));
            checker.Dismiss(info);
            Assert.IsFalse(checker.ShouldPromptOptional(info));
            Assert.IsFalse(new VersionChecker(settings, api, new SaveManager(store)).ShouldPromptOptional(info)); // yeniden başlatmada da hatırlanır

            http.Handler = spec => FakeHttp.Ok("{\"minSupportedVersion\":\"1.0.0\",\"latestVersion\":\"1.5.0\",\"updateUrl\":\"\"}");
            Assert.IsTrue(checker.ShouldPromptOptional((await checker.CheckAsync()).Value)); // daha yeni sürüm: tekrar sorulur
        }

        [Test]
        public async Task EveryRequest_CarriesAppVersionHeader()
        {
            http.Handler = spec => FakeHttp.Ok("{\"playerId\":\"p\"}");
            await api.GetAsync<PlayerDto>("/v1/me");
            Assert.AreEqual("1.2.0", http.Requests.Last().AppVersion);
        }

        [Test]
        public async Task Http426_RaisesForceUpdateOnce()
        {
            int raised = 0;
            connectivity.UpdateRequiredDetected += () => raised++;
            http.Handler = spec => new HttpResponse { StatusCode = 426, Body = "{\"code\":\"PROTOCOL_MISMATCH\"}" };
            Result<PlayerDto> r = await api.GetAsync<PlayerDto>("/v1/me");
            await api.GetAsync<PlayerDto>("/v1/me");
            Assert.AreEqual(ErrorCode.ProtocolMismatch, r.Error);
            Assert.AreEqual(1, raised);
        }

        [Test]
        public void ServerFixture_MatchesDto()
        {
            string json = System.IO.File.ReadAllText(TestPaths.Protocol("app-version.json"));
            AppVersionDto dto = Json.Deserialize<AppVersionDto>(json);
            Assert.IsNotNull(AppVersion.Compare(dto.MinSupportedVersion, dto.LatestVersion));
            Assert.IsNotNull(dto.UpdateUrl);
        }
    }
}
