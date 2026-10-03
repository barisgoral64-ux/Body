using System.Threading.Tasks;
using MinikDuello.Core;
using MinikDuello.Domain.Net;
using MinikDuello.Services.Api;
using MinikDuello.Services.Save;

namespace MinikDuello.Services.Managers
{
    public sealed class PlayerChanged
    {
    }

    /// <summary>Oyuncu profili. Çevrimdışıyken yerel önbellek gösterilir.</summary>
    public sealed class PlayerManager
    {
        private readonly ApiClient api;
        private readonly SaveManager save;
        private readonly AuthSession auth;

        public PlayerManager(ApiClient api, SaveManager save, AuthSession auth)
        {
            this.api = api;
            this.save = save;
            this.auth = auth;
        }

        public string Username => save.Data.CachedUsername ?? auth.Username ?? string.Empty;
        public string FriendCode => save.Data.CachedFriendCode ?? auth.FriendCode ?? string.Empty;
        public string Character => save.Data.CachedCharacter;
        public int Stars => save.Data.CachedStars;
        public int Coins => save.Data.CachedCoins;
        public int Level => save.Data.CachedLevel;

        public async Task<Result<PlayerDto>> RefreshAsync()
        {
            Result<PlayerDto> result = await api.GetAsync<PlayerDto>("/v1/me");
            if (!result.IsOk) return result;
            PlayerDto p = result.Value;
            save.Update(d =>
            {
                d.CachedUsername = p.Username;
                d.CachedFriendCode = p.FriendCode;
                d.CachedCharacter = string.IsNullOrEmpty(p.AvatarCharacter) ? "panda" : p.AvatarCharacter;
                d.CachedStars = p.TotalStars;
                d.CachedCoins = p.Coins;
                d.CachedLevel = p.Level;
            });
            EventBus.Publish(new PlayerChanged());
            return result;
        }
    }
}
