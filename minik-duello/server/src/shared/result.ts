export const ErrorCode = {
  InvalidInput: "INVALID_INPUT",
  Unauthorized: "UNAUTHORIZED",
  Forbidden: "FORBIDDEN",
  NotFound: "NOT_FOUND",
  RateLimited: "RATE_LIMITED",
  FriendsDisabled: "FRIENDS_DISABLED",
  MultiplayerDisabled: "MULTIPLAYER_DISABLED",
  InvitesDisabled: "INVITES_DISABLED",
  Blocked: "BLOCKED",
  AlreadyFriends: "ALREADY_FRIENDS",
  RequestPending: "REQUEST_PENDING",
  RequestExpired: "REQUEST_EXPIRED",
  FriendLimitReached: "FRIEND_LIMIT_REACHED",
  InviteExpired: "INVITE_EXPIRED",
  OpponentUnavailable: "OPPONENT_UNAVAILABLE",
  InvalidState: "INVALID_STATE",
  ProtocolMismatch: "PROTOCOL_MISMATCH",
  Internal: "INTERNAL",
} as const;

export type ErrorCode = (typeof ErrorCode)[keyof typeof ErrorCode];

export interface AppError {
  readonly code: ErrorCode;
  /** Geliştirici mesajı; çocuğa gösterilmez (istemci kodu ikon/ses seçer). */
  readonly message: string;
}

export type Result<T> = { readonly ok: true; readonly value: T } | { readonly ok: false; readonly error: AppError };

export const ok = <T>(value: T): Result<T> => ({ ok: true, value });
export const fail = <T = never>(code: ErrorCode, message: string): Result<T> => ({
  ok: false,
  error: { code, message },
});
