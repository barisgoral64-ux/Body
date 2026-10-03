using System.Collections.Generic;
using System.Threading.Tasks;
using MinikDuello.Core;
using MinikDuello.Domain.Net;
using MinikDuello.Domain.Rewards;
using MinikDuello.Services.Api;
using MinikDuello.Services.Save;

namespace MinikDuello.Services.Managers
{
    /// <summary>Envanter, günlük ödül, mağaza (yalnızca oyunda kazanılan coin) ve kuşanma. Gerçek para yoktur.</summary>
    public sealed class RewardManager
    {
        private readonly ApiClient api;
        private readonly SaveManager save;
        private readonly PlayerManager player;

        public RewardManager(ApiClient api, SaveManager save, PlayerManager player)
        {
            this.api = api;
            this.save = save;
            this.player = player;
        }

        public bool Owns(string rewardId) => save.Data.OwnedRewards.Contains(rewardId);
        public bool IsEquipped(string rewardId) => save.Data.EquippedRewards.Contains(rewardId);
        public IReadOnlyList<string> Owned => save.Data.OwnedRewards;

        public async Task<Result<InventoryDto>> RefreshInventoryAsync()
        {
            Result<InventoryDto> result = await api.GetAsync<InventoryDto>("/v1/rewards/inventory");
            if (!result.IsOk) return result;
            save.Update(d =>
            {
                d.OwnedRewards = new List<string>();
                d.EquippedRewards = new List<string>();
                foreach (InventoryItemDto item in result.Value.Items)
                {
                    d.OwnedRewards.Add(item.RewardId);
                    if (item.Equipped) d.EquippedRewards.Add(item.RewardId);
                }
            });
            return result;
        }

        public Task<Result<DailyStatusDto>> DailyStatusAsync() => api.GetAsync<DailyStatusDto>("/v1/rewards/daily");

        public async Task<Result<DailyClaimDto>> ClaimDailyAsync()
        {
            Result<DailyClaimDto> result = await api.PostAsync<DailyClaimDto>("/v1/rewards/daily/claim", new object());
            if (result.IsOk)
            {
                await player.RefreshAsync();
                await RefreshInventoryAsync();
            }
            return result;
        }

        public async Task<Result<BuyResultDto>> BuyAsync(string rewardId)
        {
            Result<BuyResultDto> result = await api.PostAsync<BuyResultDto>("/v1/rewards/buy", new RewardRequestDto { RewardId = rewardId });
            if (result.IsOk)
            {
                await player.RefreshAsync();
                await RefreshInventoryAsync();
            }
            return result;
        }

        public async Task<Result<Unit>> EquipAsync(string rewardId, bool equipped)
        {
            Result<Unit> result = await api.PostAsync<Unit>("/v1/rewards/equip", new RewardRequestDto { RewardId = rewardId, Equipped = equipped });
            if (result.IsOk)
            {
                await player.RefreshAsync();
                await RefreshInventoryAsync();
            }
            return result;
        }
    }
}
