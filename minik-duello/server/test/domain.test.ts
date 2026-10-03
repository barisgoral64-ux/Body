import { describe, expect, it } from "vitest";
import { FRIENDS, SCORING } from "../src/config/constants.js";
import { createFriendRequest, orderFriendPair, respondToRequest } from "../src/domain/friendRequest.js";
import { generateFriendCode, generateUsername, normalizeFriendCode } from "../src/domain/identity.js";
import { defaultParentSettings } from "../src/domain/models.js";
import { isQuickChatId } from "../src/domain/quickChat.js";
import { scoreAnswer, speedBonus } from "../src/domain/scoring.js";
import { ErrorCode } from "../src/shared/result.js";

const NOW = new Date("2026-01-01T00:00:00Z");

describe("scoring", () => {
  it("yanlış cevap puan vermez ve negatif olmaz", () => {
    const r = scoreAnswer({ correct: false, responseMs: 2000, roundDurationMs: 20000, currentStreak: 3 });
    expect(r).toEqual({ points: 0, newStreak: 0 });
  });
  it("doğru cevap taban + hız bonusu verir, ilk cevapta seri bonusu yok", () => {
    const r = scoreAnswer({ correct: true, responseMs: 1000, roundDurationMs: 20000, currentStreak: 0 });
    expect(r.points).toBeGreaterThanOrEqual(SCORING.correctAnswer + SCORING.speedBonusMin);
    expect(r.points).toBeLessThanOrEqual(SCORING.correctAnswer + SCORING.speedBonusMax);
    expect(r.newStreak).toBe(1);
  });
  it("seri bonusu eklenir", () => {
    const a = scoreAnswer({ correct: true, responseMs: 5000, roundDurationMs: 20000, currentStreak: 0 });
    const b = scoreAnswer({ correct: true, responseMs: 5000, roundDurationMs: 20000, currentStreak: 1 });
    expect(b.points - a.points).toBe(SCORING.streakBonus);
  });
  it("insan altı hızda hız bonusu yok", () => {
    expect(speedBonus(SCORING.minHumanResponseMs - 1, 20000)).toBe(0);
  });
  it("süre sınırında bonus minimuma iner", () => {
    expect(speedBonus(20000, 20000)).toBe(SCORING.speedBonusMin);
  });
});

describe("identity", () => {
  it("arkadaş kodu biçimi doğru ve normalleşir", () => {
    const code = generateFriendCode();
    expect(normalizeFriendCode(code)).toBe(code);
    expect(normalizeFriendCode(` ${code.toLowerCase()} `)).toBe(code);
  });
  it("geçersiz kodlar reddedilir", () => {
    expect(normalizeFriendCode("PANDA-0001")).toBeNull();
    expect(normalizeFriendCode("HACKER-4832")).toBeNull();
    expect(normalizeFriendCode("PANDA-48")).toBeNull();
    expect(normalizeFriendCode("<script>")).toBeNull();
  });
  it("kullanıcı adı yalnızca beyaz listeden üretilir", () => {
    for (let i = 0; i < 50; i += 1) expect(generateUsername()).toMatch(/^[A-Za-z]+\d{1,2}$/);
  });
});

describe("friend request", () => {
  const make = () => {
    const r = createFriendRequest("r1", "A", "B", NOW);
    if (!r.ok) throw new Error("beklenmedik");
    return r.value;
  };
  it("kendine istek gönderilemez", () => {
    const r = createFriendRequest("r", "A", "A", NOW);
    expect(r.ok).toBe(false);
  });
  it("yalnızca alıcı kabul edebilir", () => {
    const r = respondToRequest(make(), "A", true, NOW);
    expect(r.ok === false && r.error.code).toBe(ErrorCode.Forbidden);
  });
  it("alıcı kabul eder", () => {
    const r = respondToRequest(make(), "B", true, NOW);
    expect(r.ok && r.value.status).toBe("accepted");
  });
  it("süresi dolan istek kabul edilemez", () => {
    const later = new Date(NOW.getTime() + FRIENDS.requestExpiresInMs + 1);
    const r = respondToRequest(make(), "B", true, later);
    expect(r.ok === false && r.error.code).toBe(ErrorCode.RequestExpired);
  });
  it("yanıtlanmış istek tekrar yanıtlanamaz", () => {
    const first = respondToRequest(make(), "B", false, NOW);
    if (!first.ok) throw new Error("beklenmedik");
    const second = respondToRequest(first.value, "B", true, NOW);
    expect(second.ok === false && second.error.code).toBe(ErrorCode.InvalidState);
  });
  it("arkadaş çifti sıralıdır", () => {
    expect(orderFriendPair("b", "a")).toEqual(["a", "b"]);
  });
});

describe("güvenlik varsayılanları", () => {
  it("sosyal özellikler varsayılan KAPALI", () => {
    const s = defaultParentSettings("p");
    expect([s.friendsEnabled, s.multiplayerEnabled, s.onlineStatusVisible, s.gameInvitationsEnabled]).toEqual([
      false, false, false, false,
    ]);
  });
  it("serbest metin hazır mesaj olarak kabul edilmez", () => {
    expect(isQuickChatId("hello")).toBe(true);
    expect(isQuickChatId("merhaba benim adım Ali")).toBe(false);
    expect(isQuickChatId(42)).toBe(false);
  });
});
