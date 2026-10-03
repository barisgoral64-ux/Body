import { FRIENDS } from "../config/constants.js";
import { ErrorCode, fail, ok, type Result } from "../shared/result.js";
import type { FriendRequest, PlayerId } from "./models.js";

export function createFriendRequest(
  requestId: string,
  senderPlayerId: PlayerId,
  receiverPlayerId: PlayerId,
  now: Date,
): Result<FriendRequest> {
  if (senderPlayerId === receiverPlayerId) {
    return fail(ErrorCode.InvalidInput, "Kendine istek gönderilemez");
  }
  return ok({
    requestId,
    senderPlayerId,
    receiverPlayerId,
    status: "pending",
    createdAt: now,
    expiresAt: new Date(now.getTime() + FRIENDS.requestExpiresInMs),
  });
}

/** pending ve süresi dolmuşsa expired döndürür; diğer durumlar değişmez. */
export function refreshRequestStatus(request: FriendRequest, now: Date): FriendRequest {
  if (request.status === "pending" && now.getTime() >= request.expiresAt.getTime()) {
    return { ...request, status: "expired" };
  }
  return request;
}

/** Yalnızca alıcı, yalnızca pending ve süresi dolmamış isteği kabul/red edebilir. */
export function respondToRequest(
  request: FriendRequest,
  responderId: PlayerId,
  accept: boolean,
  now: Date,
): Result<FriendRequest> {
  const current = refreshRequestStatus(request, now);
  if (responderId !== current.receiverPlayerId) {
    return fail(ErrorCode.Forbidden, "Yalnızca alıcı yanıtlayabilir");
  }
  if (current.status === "expired") return fail(ErrorCode.RequestExpired, "İstek süresi doldu");
  if (current.status !== "pending") return fail(ErrorCode.InvalidState, "İstek zaten yanıtlanmış");
  return ok({ ...current, status: accept ? "accepted" : "rejected" });
}

/** Arkadaşlık çifti tek satırdır: küçük ID her zaman ilk sıradadır. */
export function orderFriendPair(a: PlayerId, b: PlayerId): readonly [PlayerId, PlayerId] {
  return a < b ? [a, b] : [b, a];
}
