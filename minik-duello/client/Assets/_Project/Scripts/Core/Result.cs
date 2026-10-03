namespace MinikDuello.Core
{
    /// <summary>Sunucu hata kodlarıyla birebir aynı (server/src/shared/result.ts).</summary>
    public enum ErrorCode
    {
        None,
        InvalidInput,
        Unauthorized,
        Forbidden,
        NotFound,
        RateLimited,
        FriendsDisabled,
        MultiplayerDisabled,
        InvitesDisabled,
        Blocked,
        AlreadyFriends,
        RequestPending,
        RequestExpired,
        FriendLimitReached,
        InviteExpired,
        OpponentUnavailable,
        InvalidState,
        ProtocolMismatch,
        Network,
        Internal
    }

    public readonly struct Result<T>
    {
        public bool IsOk { get; }
        public T Value { get; }
        public ErrorCode Error { get; }
        public string Message { get; }

        private Result(bool isOk, T value, ErrorCode error, string message)
        {
            IsOk = isOk;
            Value = value;
            Error = error;
            Message = message;
        }

        public static Result<T> Ok(T value) => new Result<T>(true, value, ErrorCode.None, string.Empty);

        public static Result<T> Fail(ErrorCode error, string message) =>
            new Result<T>(false, default, error, message);
    }
}
