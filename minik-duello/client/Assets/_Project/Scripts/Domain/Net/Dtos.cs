using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MinikDuello.Domain.Net
{
    /// <summary>Tüm JSON işlemleri aynı ayarı kullanır (camelCase, bilinmeyen alanlar yok sayılır).</summary>
    public static class Json
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore
        };

        public static string Serialize(object value) => JsonConvert.SerializeObject(value, Settings);

        public static T Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(json, Settings);
    }

    public sealed class TokensDto
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
    }

    public sealed class RegisterPlayerDto
    {
        public string PlayerId { get; set; }
        public string Username { get; set; }
        public string FriendCode { get; set; }
    }

    public sealed class RegisterResponseDto
    {
        public TokensDto Tokens { get; set; }
        public bool IsNew { get; set; }
        public RegisterPlayerDto Player { get; set; }
    }

    public sealed class PlayerDto
    {
        public string PlayerId { get; set; }
        public string Username { get; set; }
        public string FriendCode { get; set; }
        public string AvatarCharacter { get; set; }
        public string AvatarFrame { get; set; }
        public int Level { get; set; }
        public int TotalStars { get; set; }
        public int Coins { get; set; }
    }

    public sealed class PublicPlayerDto
    {
        public string PlayerId { get; set; }
        public string Username { get; set; }
        public string AvatarCharacter { get; set; }
        public string AvatarFrame { get; set; }
    }

    public sealed class FriendDto
    {
        public string PlayerId { get; set; }
        public string Username { get; set; }
        public string AvatarCharacter { get; set; }
        public string AvatarFrame { get; set; }
        /// <summary>online | playing | offline</summary>
        public string Presence { get; set; }
    }

    public sealed class FriendListDto
    {
        public List<FriendDto> Friends { get; set; } = new List<FriendDto>();
    }

    public sealed class IncomingRequestDto
    {
        public string RequestId { get; set; }
        public PublicPlayerDto From { get; set; }
        public string CreatedAt { get; set; }
        public string ExpiresAt { get; set; }
    }

    public sealed class IncomingRequestsDto
    {
        public List<IncomingRequestDto> Requests { get; set; } = new List<IncomingRequestDto>();
    }

    public sealed class ParentSettingsDto
    {
        public bool FriendsEnabled { get; set; }
        public bool MultiplayerEnabled { get; set; }
        public bool OnlineStatusVisible { get; set; }
        public bool GameInvitationsEnabled { get; set; }
        public int DailyLimitMinutes { get; set; }
        public bool SoundEnabled { get; set; } = true;
        public bool HasPin { get; set; }
    }

    public sealed class LevelProgressDto
    {
        public int LevelId { get; set; }
        public int Stars { get; set; }
        public int BestScore { get; set; }
        public bool Unlocked { get; set; }
    }

    public sealed class ProgressDto
    {
        public List<LevelProgressDto> Levels { get; set; } = new List<LevelProgressDto>();
    }

    public sealed class LevelResultRequestDto
    {
        public int LevelId { get; set; }
        public int Score { get; set; }
        public int DurationMs { get; set; }
    }

    public sealed class LevelResultDto
    {
        public int Stars { get; set; }
        public int StarsGained { get; set; }
        public int CoinsGained { get; set; }
        public List<string> NewRewards { get; set; } = new List<string>();
        public int? NextLevelId { get; set; }
    }

    public sealed class DailyStatusDto
    {
        public bool CanClaim { get; set; }
        public int NextDayIndex { get; set; }
        public int CycleLength { get; set; }
    }

    public sealed class DailyClaimDto
    {
        public int Coins { get; set; }
        public string RewardId { get; set; }
        public int DayIndex { get; set; }
    }

    public sealed class InventoryItemDto
    {
        public string RewardId { get; set; }
        public bool Equipped { get; set; }
    }

    public sealed class InventoryDto
    {
        public List<InventoryItemDto> Items { get; set; } = new List<InventoryItemDto>();
    }

    public sealed class WeeklyEntryDto
    {
        public string PlayerId { get; set; }
        public string Username { get; set; }
        public string AvatarCharacter { get; set; }
        public int Stars { get; set; }
        public bool IsSelf { get; set; }
    }

    public sealed class WeeklyDto
    {
        public List<WeeklyEntryDto> Entries { get; set; } = new List<WeeklyEntryDto>();
    }

    public sealed class ErrorDto
    {
        public string Code { get; set; }
    }

    public sealed class FriendCodeRequestDto
    {
        public string FriendCode { get; set; }
    }

    public sealed class RespondRequestDto
    {
        public bool Accept { get; set; }
    }

    public sealed class RewardRequestDto
    {
        public string RewardId { get; set; }
        public bool? Equipped { get; set; }
    }

    public sealed class BuyResultDto
    {
        public int Coins { get; set; }
    }

    public sealed class SaveBlobDto
    {
        public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();
    }
}
