using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MinikDuello.Core;
using MinikDuello.Domain.Net;
using MinikDuello.Services.Api;

namespace MinikDuello.Services.Managers
{
    public sealed class FriendsChanged
    {
    }

    /// <summary>Arkadaş listesi, istekler ve çevrimiçi durum. İstek gönderme/kabul UI'da yetişkin kapısının arkasındadır.</summary>
    public sealed class FriendManager
    {
        private readonly ApiClient api;
        private readonly List<FriendDto> friends = new List<FriendDto>();
        private readonly Dictionary<string, string> presence = new Dictionary<string, string>();

        public FriendManager(ApiClient api)
        {
            this.api = api;
        }

        public IReadOnlyList<FriendDto> Friends => friends;

        public string PresenceOf(string playerId) => presence.TryGetValue(playerId, out string s) ? s : "offline";

        public async Task<Result<IReadOnlyList<FriendDto>>> RefreshAsync()
        {
            Result<FriendListDto> result = await api.GetAsync<FriendListDto>("/v1/friends");
            if (!result.IsOk) return Result<IReadOnlyList<FriendDto>>.Fail(result.Error, result.Message);
            friends.Clear();
            presence.Clear();
            foreach (FriendDto f in result.Value.Friends)
            {
                friends.Add(f);
                presence[f.PlayerId] = f.Presence ?? "offline";
            }
            EventBus.Publish(new FriendsChanged());
            return Result<IReadOnlyList<FriendDto>>.Ok(friends);
        }

        public async Task<Result<IReadOnlyList<IncomingRequestDto>>> IncomingAsync()
        {
            Result<IncomingRequestsDto> result = await api.GetAsync<IncomingRequestsDto>("/v1/friends/requests/incoming");
            if (!result.IsOk) return Result<IReadOnlyList<IncomingRequestDto>>.Fail(result.Error, result.Message);
            return Result<IReadOnlyList<IncomingRequestDto>>.Ok(result.Value.Requests);
        }

        /// <summary>Arkadaş kodu ile istek. Sunucu, kod bulunamasa bile nötr başarı döner (hesap varlığı sızdırılmaz).</summary>
        public Task<Result<Unit>> SendRequestAsync(string friendCode) =>
            api.PostAsync<Unit>("/v1/friends/requests", new FriendCodeRequestDto { FriendCode = (friendCode ?? string.Empty).Trim() });

        public async Task<Result<Unit>> RespondAsync(string requestId, bool accept)
        {
            Result<Unit> result = await api.PostAsync<Unit>("/v1/friends/requests/" + Uri.EscapeDataString(requestId) + "/respond", new RespondRequestDto { Accept = accept });
            if (result.IsOk && accept) await RefreshAsync();
            return result;
        }

        public async Task<Result<IReadOnlyList<WeeklyEntryDto>>> WeeklyAsync()
        {
            Result<WeeklyDto> result = await api.GetAsync<WeeklyDto>("/v1/leaderboard/weekly");
            if (!result.IsOk) return Result<IReadOnlyList<WeeklyEntryDto>>.Fail(result.Error, result.Message);
            return Result<IReadOnlyList<WeeklyEntryDto>>.Ok(result.Value.Entries);
        }

        /// <summary>Gerçek zamanlı bildirimlerden gelen durum güncellemesi.</summary>
        public void ApplyPresence(PresenceUpdateDto update)
        {
            presence[update.PlayerId] = update.Status ?? "offline";
            foreach (FriendDto f in friends)
            {
                if (f.PlayerId == update.PlayerId) f.Presence = update.Status;
            }
            EventBus.Publish(new FriendsChanged());
        }
    }
}
