using MinikDuello.Core;

namespace MinikDuello.Services
{
    /// <summary>Sunucu hata kodlarını (server/src/shared/result.ts) istemci ErrorCode'una çevirir.</summary>
    public static class ErrorCodes
    {
        public static ErrorCode FromServer(string code, int httpStatus)
        {
            switch (code)
            {
                case "INVALID_INPUT": return ErrorCode.InvalidInput;
                case "UNAUTHORIZED": return ErrorCode.Unauthorized;
                case "FORBIDDEN": return ErrorCode.Forbidden;
                case "NOT_FOUND": return ErrorCode.NotFound;
                case "RATE_LIMITED": return ErrorCode.RateLimited;
                case "FRIENDS_DISABLED": return ErrorCode.FriendsDisabled;
                case "MULTIPLAYER_DISABLED": return ErrorCode.MultiplayerDisabled;
                case "INVITES_DISABLED": return ErrorCode.InvitesDisabled;
                case "BLOCKED": return ErrorCode.Blocked;
                case "ALREADY_FRIENDS": return ErrorCode.AlreadyFriends;
                case "REQUEST_PENDING": return ErrorCode.RequestPending;
                case "REQUEST_EXPIRED": return ErrorCode.RequestExpired;
                case "FRIEND_LIMIT_REACHED": return ErrorCode.FriendLimitReached;
                case "INVITE_EXPIRED": return ErrorCode.InviteExpired;
                case "OPPONENT_UNAVAILABLE": return ErrorCode.OpponentUnavailable;
                case "INVALID_STATE": return ErrorCode.InvalidState;
                case "PROTOCOL_MISMATCH": return ErrorCode.ProtocolMismatch;
                case "INTERNAL": return ErrorCode.Internal;
                default: return httpStatus == 401 ? ErrorCode.Unauthorized : ErrorCode.Internal;
            }
        }
    }
}
