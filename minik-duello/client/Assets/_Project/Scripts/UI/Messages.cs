using MinikDuello.Core;

namespace MinikDuello.UI
{
    /// <summary>Hata kodunu çocuğa uygun, olumsuzluk içermeyen kısa metne çevirir.</summary>
    public static class Messages
    {
        public static string For(ErrorCode code)
        {
            switch (code)
            {
                case ErrorCode.Network: return Strings.OfflineNote;
                case ErrorCode.RateLimited: return Strings.PleaseWait;
                case ErrorCode.FriendsDisabled:
                case ErrorCode.MultiplayerDisabled:
                case ErrorCode.InvitesDisabled: return Strings.ParentNeedsToEnable;
                case ErrorCode.Blocked:
                case ErrorCode.OpponentUnavailable: return "Şu an oynayamıyor.";
                case ErrorCode.AlreadyFriends: return "Zaten arkadaşsınız!";
                case ErrorCode.FriendLimitReached: return "Arkadaş listesi dolu.";
                case ErrorCode.RequestExpired: return "Bu isteğin süresi dolmuş.";
                case ErrorCode.InviteExpired: return "Davetin süresi doldu.";
                case ErrorCode.Forbidden: return "PIN doğru değil.";
                default: return Strings.TryAgain;
            }
        }

        /// <summary>Sunucu hata kodu (WebSocket) için aynı eşleme.</summary>
        public static string ForServer(string serverCode) =>
            For(MinikDuello.Services.ErrorCodes.FromServer(serverCode, 0));
    }
}
