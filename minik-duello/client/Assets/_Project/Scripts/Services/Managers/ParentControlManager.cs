using System.Threading.Tasks;
using MinikDuello.Core;
using MinikDuello.Domain.Net;
using MinikDuello.Services.Api;

namespace MinikDuello.Services.Managers
{
    public sealed class ParentSettingsChanged
    {
    }

    /// <summary>Ebeveyn paneli işlemleri. PIN doğrulaması sunucudadır; yanlış denemede sunucu kilitler.</summary>
    public sealed class ParentControlManager
    {
        private readonly ApiClient api;

        public ParentControlManager(ApiClient api)
        {
            this.api = api;
        }

        /// <summary>Son bilinen ayarlar; ayarlar alınamadıysa güvenli varsayılan: sosyal özellikler KAPALI.</summary>
        public ParentSettingsDto Settings { get; private set; } = new ParentSettingsDto();

        public bool SocialAvailable => Settings.FriendsEnabled;
        public bool MultiplayerAvailable => Settings.FriendsEnabled && Settings.MultiplayerEnabled;

        public async Task<Result<ParentSettingsDto>> RefreshAsync()
        {
            Result<ParentSettingsDto> result = await api.GetAsync<ParentSettingsDto>("/v1/parent/settings");
            if (result.IsOk)
            {
                Settings = result.Value;
                EventBus.Publish(new ParentSettingsChanged());
            }
            return result;
        }

        public async Task<Result<ParentSettingsDto>> UpdateAsync(object patch, string pin)
        {
            Result<ParentSettingsDto> result = await api.PutAsync<ParentSettingsDto>("/v1/parent/settings", new { pin, patch });
            if (result.IsOk)
            {
                Settings = result.Value;
                EventBus.Publish(new ParentSettingsChanged());
            }
            return result;
        }

        public async Task<Result<Unit>> SetPinAsync(string newPin, string currentPin)
        {
            Result<Unit> result = await api.PutAsync<Unit>("/v1/parent/pin", new { newPin, currentPin });
            if (result.IsOk) await RefreshAsync();
            return result;
        }

        /// <summary>
        /// PIN'i doğrulamak için zararsız (boş) bir ayar çağrısı kullanılır. PIN hiç belirlenmemişse sunucu boş yamayı
        /// doğrulamadan kabul eder; bu yüzden PIN yokken başarı DÖNMEZ (yanlış "doğrulandı" sonucu önlenir).
        /// </summary>
        public async Task<Result<ParentSettingsDto>> VerifyPinAsync(string pin)
        {
            if (!Settings.HasPin)
            {
                Result<ParentSettingsDto> fresh = await RefreshAsync();
                if (!fresh.IsOk) return fresh;
            }
            if (!Settings.HasPin) return Result<ParentSettingsDto>.Fail(ErrorCode.Forbidden, "PIN belirlenmemiş");
            return await UpdateAsync(new { }, pin);
        }

        public Task<Result<Unit>> BlockAsync(string playerId, string pin) =>
            api.PostAsync<Unit>("/v1/parent/block", new { pin, blockedId = playerId });

        public Task<Result<Unit>> UnblockAsync(string playerId, string pin) =>
            api.PostAsync<Unit>("/v1/parent/unblock", new { pin, blockedId = playerId });

        public Task<Result<Unit>> RemoveFriendAsync(string friendId, string pin) =>
            api.PostAsync<Unit>("/v1/parent/friends/remove", new { pin, friendId });
    }
}
