using System.Threading.Tasks;
using MinikDuello.Core;
using MinikDuello.Domain.Net;
using MinikDuello.Services.Api;
using MinikDuello.Services.Save;

namespace MinikDuello.Services.Managers
{
    public sealed class UpdateInfo
    {
        public UpdateStatus Status;
        public string CurrentVersion;
        public string LatestVersion;
        public string StoreUrl;
    }

    /// <summary>
    /// Sürüm denetimi: sunucudan asgari/en yeni sürüm alınır. Ağ yoksa güncelleme dayatılmaz (çevrimdışı oynanabilir).
    /// Mağaza adresi sunucudan gelir; yoksa paket adından Google Play adresi üretilir.
    /// </summary>
    public sealed class VersionChecker
    {
        public const string PlayStoreFormat = "https://play.google.com/store/apps/details?id={0}";

        private readonly AppSettings settings;
        private readonly ApiClient api;
        private readonly SaveManager save;

        public UpdateInfo Last { get; private set; }

        public VersionChecker(AppSettings settings, ApiClient api, SaveManager save)
        {
            this.settings = settings;
            this.api = api;
            this.save = save;
        }

        public string CurrentVersion => settings.AppVersion;

        public async Task<Result<UpdateInfo>> CheckAsync()
        {
            Result<AppVersionDto> result = await api.GetAsync<AppVersionDto>("/v1/app-version");
            if (!result.IsOk)
            {
                // 426 zaten sürümün desteklenmediği anlamına gelir; diğer hatalarda güncelleme dayatılmaz.
                if (result.Error == ErrorCode.ProtocolMismatch)
                    return Remember(new UpdateInfo { Status = UpdateStatus.Required, CurrentVersion = CurrentVersion, StoreUrl = BuildStoreUrl(null) });
                return Result<UpdateInfo>.Fail(result.Error, result.Message);
            }

            AppVersionDto policy = result.Value;
            return Remember(new UpdateInfo
            {
                Status = AppVersion.Evaluate(CurrentVersion, policy),
                CurrentVersion = CurrentVersion,
                LatestVersion = policy.LatestVersion,
                StoreUrl = BuildStoreUrl(policy.UpdateUrl)
            });
        }

        /// <summary>İsteğe bağlı güncelleme için kullanıcıya bu sürümde daha önce sorulduysa false.</summary>
        public bool ShouldPromptOptional(UpdateInfo info) =>
            info.Status == UpdateStatus.Available && save.Data.DismissedUpdateVersion != info.LatestVersion;

        public void Dismiss(UpdateInfo info) => save.Update(d => d.DismissedUpdateVersion = info.LatestVersion);

        private string BuildStoreUrl(string serverUrl)
        {
            if (!string.IsNullOrWhiteSpace(serverUrl)) return serverUrl;
            return string.IsNullOrEmpty(settings.PackageName) ? string.Empty : string.Format(PlayStoreFormat, settings.PackageName);
        }

        private Result<UpdateInfo> Remember(UpdateInfo info)
        {
            Last = info;
            return Result<UpdateInfo>.Ok(info);
        }
    }
}
