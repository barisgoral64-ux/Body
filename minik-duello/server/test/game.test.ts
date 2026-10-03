import { describe, expect, it } from "vitest";
import { REWARDS, ROOM, ROUND_TIMING, SCORING } from "../src/config/constants.js";
import type { GameRoom } from "../src/game/gameRoom.js";
import type { RoundView } from "../src/protocol.js";
import { correctChoices, createEnv, messagesTo, newPlayer, wrongChoice, type TestEnv, type TestPlayer } from "./helpers.js";

export function currentRound(env: TestEnv, playerId: string): RoundView {
  const msgs = messagesTo(env, playerId, "room.round");
  const last = msgs[msgs.length - 1];
  if (!last) throw new Error("tur yok");
  return last.payload as unknown as RoundView;
}

async function setup(mode: Parameters<TestEnv["c"]["rooms"]["createRoom"]>[2], seed = 1) {
  const env = await createEnv(seed);
  const a = await newPlayer(env);
  const b = await newPlayer(env);
  const created = env.c.rooms.createRoom(a.id, b.id, mode);
  if (!created.ok) throw new Error("oda");
  return { env, a, b, room: created.value };
}

function startMatch(env: TestEnv, room: GameRoom, a: TestPlayer, b: TestPlayer): void {
  room.setReady(a.id);
  room.setReady(b.id);
  env.time.advance(ROOM.countdownSeconds * 1000);
}

/** Aktif turu oynatır: p1 hızlı, p2 yavaş cevap verir. */
function playRound(env: TestEnv, room: GameRoom, a: TestPlayer, b: TestPlayer, opts: { aRight: boolean; bRight: boolean }): void {
  const view = currentRound(env, a.id);
  env.time.advance(view.showMs + 1000);
  const give = (p: TestPlayer, right: boolean): void => {
    const picks = right ? correctChoices(view) : [wrongChoice(view)];
    for (const choice of picks) room.answer(p.id, view.roundId, choice);
  };
  give(a, opts.aRight);
  env.time.advance(4000);
  give(b, opts.bRight);
  env.time.advance(ROUND_TIMING.betweenRoundsMs);
}

describe("rekabetçi maç", () => {
  it("tam maç: sunucu puanlar, kazanan daha fazla ödül alır, kaybeden de ödül alır", async () => {
    const { env, a, b, room } = await setup("mixedMatch");
    startMatch(env, room, a, b);
    expect(room.state).toBe("playing");
    // Aynı görev iki oyuncuya da gider.
    expect(currentRound(env, a.id).roundId).toBe(currentRound(env, b.id).roundId);

    for (let i = 0; i < 5; i += 1) playRound(env, room, a, b, { aRight: true, bRight: i % 2 === 0 });
    await room.persisted;

    expect(room.state).toBe("finished");
    const fin = messagesTo(env, a.id, "room.finished")[0]?.payload as { outcomes: { playerId: string; isWinner: boolean; starsAwarded: number; score: number }[] };
    const oa = fin.outcomes.find((o) => o.playerId === a.id);
    const ob = fin.outcomes.find((o) => o.playerId === b.id);
    expect(oa?.isWinner).toBe(true);
    expect(ob?.isWinner).toBe(false);
    expect(oa?.starsAwarded).toBe(REWARDS.matchWinnerStars);
    expect(ob?.starsAwarded).toBe(REWARDS.matchParticipantStars);
    expect(ob?.score).toBeGreaterThan(0);
    expect((oa?.score ?? 0) > (ob?.score ?? 0)).toBe(true);

    const meA = await env.c.players.getSelf(a.id);
    expect(meA.ok && meA.value.totalStars).toBe(REWARDS.matchWinnerStars);
    expect(await env.matchCount()).toBe(1);
    expect(env.c.rooms.roomOf(a.id)).toBeNull(); // oda temizlendi
  });

  it("puan: doğru ≥ 110, seri bonusu, yanlış 0, negatif yok", async () => {
    const { env, a, b, room } = await setup("colorRace");
    startMatch(env, room, a, b);
    const v1 = currentRound(env, a.id);
    env.time.advance(500);
    const r1 = room.answer(a.id, v1.roundId, correctChoices(v1)[0] as string);
    expect(r1.ok && r1.value.points).toBeGreaterThanOrEqual(SCORING.correctAnswer + SCORING.speedBonusMin);
    expect(r1.ok && r1.value.points).toBeLessThanOrEqual(SCORING.correctAnswer + SCORING.speedBonusMax);
    room.answer(b.id, v1.roundId, wrongChoice(v1));
    env.time.advance(ROUND_TIMING.betweenRoundsMs);

    const v2 = currentRound(env, a.id);
    env.time.advance(500);
    const r2 = room.answer(a.id, v2.roundId, correctChoices(v2)[0] as string);
    expect(r2.ok && r2.value.points).toBeGreaterThanOrEqual(SCORING.correctAnswer + SCORING.streakBonus);
    const wrong = room.answer(b.id, v2.roundId, wrongChoice(v2));
    expect(wrong.ok && wrong.value.points).toBe(0);
    expect(wrong.ok && wrong.value.total).toBeGreaterThanOrEqual(0);
  });

  it("hile korumaları: eski/yanlış roundId, çift cevap, üye olmayan, geçersiz seçim", async () => {
    const { env, a, b, room } = await setup("colorRace");
    const stranger = await newPlayer(env);
    startMatch(env, room, a, b);
    const v = currentRound(env, a.id);
    env.time.advance(300);
    const right = correctChoices(v)[0] as string;
    expect(room.answer(a.id, "eski-tur", right).ok).toBe(false);
    expect(room.answer(stranger.id, v.roundId, right).ok).toBe(false);
    expect(room.answer(a.id, v.roundId, "olmayan-secim").ok).toBe(false);
    expect(room.answer(a.id, v.roundId, right).ok).toBe(true);
    const second = room.answer(a.id, v.roundId, right);
    expect(second.ok).toBe(false); // çift cevap
    expect(room.setReady(stranger.id).ok).toBe(false);
  });

  it("hafıza turunda gösterim bitmeden cevap kabul edilmez", async () => {
    const { env, a, b, room } = await setup("memoryDuel");
    startMatch(env, room, a, b);
    const v = currentRound(env, a.id);
    expect(v.showMs).toBe(ROUND_TIMING.memoryShowMs);
    env.time.advance(500);
    expect(room.answer(a.id, v.roundId, correctChoices(v)[0] as string).ok).toBe(false);
    env.time.advance(v.showMs);
    expect(room.answer(a.id, v.roundId, correctChoices(v)[0] as string).ok).toBe(true);
  });

  it("süre dolunca tur kimse ceza almadan biter ve sonraki tura geçilir", async () => {
    const { env, a, b, room } = await setup("colorRace");
    startMatch(env, room, a, b);
    const first = currentRound(env, a.id).roundId;
    env.time.advance(first.length > 0 ? 15_000 + ROUND_TIMING.betweenRoundsMs : 0);
    expect(currentRound(env, a.id).roundId).not.toBe(first);
    const result = messagesTo(env, a.id, "room.roundResult")[0]?.payload as { scores: Record<string, number> };
    expect(Object.values(result.scores)).toEqual([0, 0]);
  });

  it("beraberlikte iki oyuncu da kazanan sayılır", async () => {
    const { env, a, b, room } = await setup("colorRace");
    startMatch(env, room, a, b);
    for (let i = 0; i < 5; i += 1) env.time.advance(15_000 + ROUND_TIMING.betweenRoundsMs);
    await room.persisted;
    const fin = messagesTo(env, a.id, "room.finished")[0]?.payload as { outcomes: { isWinner: boolean }[] };
    expect(fin.outcomes.every((o) => o.isWinner)).toBe(true);
  });

  it("yıldız toplama: aynı yıldızı yalnızca ilk alan kazanır", async () => {
    const { env, a, b, room } = await setup("starCollect");
    startMatch(env, room, a, b);
    const v = currentRound(env, a.id);
    env.time.advance(500);
    const [first, ...rest] = correctChoices(v) as [string, ...string[]];
    expect(room.answer(a.id, v.roundId, first).ok).toBe(true);
    const stolen = room.answer(b.id, v.roundId, first);
    expect(stolen.ok && stolen.value.taken).toBe(true);
    expect(stolen.ok && stolen.value.points).toBe(0);
    for (const s of rest) room.answer(b.id, v.roundId, s);
    const result = messagesTo(env, a.id, "room.roundResult")[0]?.payload as { claimedBy: Record<string, string> };
    expect(result.claimedBy[first]).toBe(a.id);
  });
});

describe("bağlantı kopması", () => {
  it("grace süresi dolunca maç güvenle biter, iki oyuncu da asgari ödül alır", async () => {
    const { env, a, b, room } = await setup("colorRace");
    startMatch(env, room, a, b);
    room.disconnect(b.id);
    expect(room.state).toBe("reconnecting");
    expect(messagesTo(env, a.id, "room.opponentDisconnected")).toHaveLength(1);
    env.time.advance(ROOM.reconnectGraceMs + 1);
    await room.persisted;
    expect(room.state).toBe("finished");
    const fin = messagesTo(env, a.id, "room.finished")[0]?.payload as { reason: string; outcomes: { starsAwarded: number; isWinner: boolean }[] };
    expect(fin.reason).toBe("disconnect");
    expect(fin.outcomes.every((o) => o.starsAwarded === REWARDS.matchMinimumStars && !o.isWinner)).toBe(true);
  });

  it("süre içinde dönülürse oyun kalan süreyle devam eder; kopuk süre bedava zaman vermez", async () => {
    const { env, a, b, room } = await setup("colorRace");
    startMatch(env, room, a, b);
    const token = (messagesTo(env, b.id, "room.state")[0]?.payload as { resumeToken: string }).resumeToken;
    const v = currentRound(env, a.id);
    env.time.advance(5000);
    room.disconnect(b.id);
    env.time.advance(10_000); // grace içinde
    expect(room.resume(b.id, "yanlis-anahtar").ok).toBe(false);
    expect(room.resume(b.id, token).ok).toBe(true);
    expect(room.state).toBe("playing");
    expect(messagesTo(env, a.id, "room.opponentReconnected")).toHaveLength(1);
    // Kalan süre ≈ 15sn - 5sn = 10sn: 9.5 sn sonra tur hâlâ açık, 10.5 sn sonra kapanır.
    env.time.advance(9_500);
    expect(room.answer(a.id, v.roundId, correctChoices(v)[0] as string).ok).toBe(true);
    env.time.advance(1_000);
    const result = messagesTo(env, a.id, "room.roundResult");
    expect(result).toHaveLength(1);
  });

  it("grace süresi tek sefer işler: dönen oyuncu sonradan maçı bitirmez", async () => {
    const { env, a, b, room } = await setup("colorRace");
    startMatch(env, room, a, b);
    const token = (messagesTo(env, b.id, "room.state")[0]?.payload as { resumeToken: string }).resumeToken;
    room.disconnect(a.id);
    room.resume(a.id, token === "" ? "x" : (messagesTo(env, a.id, "room.state")[0]?.payload as { resumeToken: string }).resumeToken);
    env.time.advance(ROOM.reconnectGraceMs + 1000);
    expect(room.state).toBe("playing");
  });

  it("oyun başlamadan kopma ve süre aşımı ödül üretmez", async () => {
    const { env, a, b, room } = await setup("colorRace");
    room.setReady(a.id);
    env.time.advance(ROUND_TIMING.roomWaitTimeoutMs + 1);
    await room.persisted;
    expect(room.state).toBe("finished");
    const fin = messagesTo(env, b.id, "room.finished")[0]?.payload as { reason: string; outcomes: { starsAwarded: number }[] };
    expect(fin.reason).toBe("timeout");
    expect(fin.outcomes.every((o) => o.starsAwarded === 0)).toBe(true);
  });

  it("geri sayım sırasında kopma bekleme durumuna döner", async () => {
    const { env, a, b, room } = await setup("colorRace");
    room.setReady(a.id);
    room.setReady(b.id);
    expect(room.state).toBe("ready");
    room.disconnect(b.id);
    expect(room.state).toBe("waiting");
    env.time.advance(ROOM.countdownSeconds * 1000 + 100);
    expect(room.state).toBe("waiting"); // tur başlamadı
  });

  it("oyundan çıkmak maçı kapatır, çıkan da asgari ödül alır (cezalandırma yok)", async () => {
    const { env, a, b, room } = await setup("colorRace");
    startMatch(env, room, a, b);
    room.leave(a.id);
    await room.persisted;
    const fin = messagesTo(env, a.id, "room.finished")[0]?.payload as { outcomes: { starsAwarded: number }[] };
    expect(fin.outcomes.every((o) => o.starsAwarded >= 1)).toBe(true);
  });
});

describe("co-op", () => {
  it("Yıldız modu: ortak hedefe ulaşınca iki oyuncu da takım ödülü alır", async () => {
    const { env, a, b, room } = await setup("coopStars");
    startMatch(env, room, a, b);
    for (let round = 0; round < 2; round += 1) {
      const v = currentRound(env, a.id);
      env.time.advance(500);
      const stars = correctChoices(v);
      stars.forEach((s, i) => room.answer(i % 2 === 0 ? a.id : b.id, v.roundId, s)); // A 3, B 2 gibi bölüşürler
      env.time.advance(ROUND_TIMING.betweenRoundsMs);
    }
    await room.persisted;
    expect(room.state).toBe("finished");
    const fin = messagesTo(env, b.id, "room.finished")[0]?.payload as { coop: { success: boolean; progress: number }; outcomes: { starsAwarded: number }[] };
    expect(fin.coop.success).toBe(true);
    expect(fin.coop.progress).toBeGreaterThanOrEqual(ROOM.coopTeamTargetStars);
    expect(fin.outcomes.every((o) => o.starsAwarded === REWARDS.coopSuccessStars)).toBe(true);
  });

  it("hedef tutmazsa da takım 'deneme' ödülü alır", async () => {
    const { env, a, b, room } = await setup("coopStars");
    startMatch(env, room, a, b);
    for (let i = 0; i < ROUND_TIMING.coopStarsMaxRounds; i += 1) env.time.advance(20_000 + ROUND_TIMING.betweenRoundsMs);
    await room.persisted;
    const fin = messagesTo(env, a.id, "room.finished")[0]?.payload as { coop: { success: boolean }; outcomes: { starsAwarded: number }[] };
    expect(fin.coop.success).toBe(false);
    expect(fin.outcomes.every((o) => o.starsAwarded === REWARDS.coopTryStars)).toBe(true);
  });

  it("Birlikte puzzle: tur ikisinden biri çözünce tamamlanır", async () => {
    const { env, a, b, room } = await setup("coopPuzzle");
    startMatch(env, room, a, b);
    for (let i = 0; i < ROUND_TIMING.coopPuzzleRounds; i += 1) {
      const v = currentRound(env, a.id);
      env.time.advance(1000);
      room.answer(i % 2 === 0 ? a.id : b.id, v.roundId, correctChoices(v)[0] as string);
      env.time.advance(ROUND_TIMING.betweenRoundsMs);
    }
    await room.persisted;
    const fin = messagesTo(env, a.id, "room.finished")[0]?.payload as { coop: { success: boolean } };
    expect(fin.coop.success).toBe(true);
  });
});

describe("hazır mesaj", () => {
  it("yalnızca rakibe gider, bekleme süresi uygulanır", async () => {
    const { env, a, b, room } = await setup("colorRace");
    expect(room.sendQuickChat(a.id, "hello").ok).toBe(true);
    expect(messagesTo(env, b.id, "room.quickChat")).toHaveLength(1);
    expect(messagesTo(env, a.id, "room.quickChat")).toHaveLength(0);
    expect(room.sendQuickChat(a.id, "great").ok).toBe(false); // çok hızlı
    env.time.advance(1500);
    expect(room.sendQuickChat(a.id, "great").ok).toBe(true);
  });
});

describe("oda yönetimi", () => {
  it("bir oyuncu aynı anda tek odada olabilir", async () => {
    const { env, a, b } = await setup("colorRace");
    const c = await newPlayer(env);
    expect(env.c.rooms.createRoom(a.id, c.id, "colorRace").ok).toBe(false);
    expect(env.c.rooms.createRoom(c.id, b.id, "colorRace").ok).toBe(false);
  });
  it("ortak odayı kapatmak güvenli biter", async () => {
    const { env, a, b, room } = await setup("colorRace");
    startMatch(env, room, a, b);
    env.c.rooms.endRoomBetween(a.id, b.id);
    await room.persisted;
    expect(room.state).toBe("finished");
    expect(env.c.rooms.activeRoomCount()).toBe(0);
  });
});

describe("yeniden bağlanma görünümü", () => {
  it("dönen oyuncuya aktif tur kalan süreyle yeniden gönderilir", async () => {
    const { env, a, b, room } = await setup("colorRace");
    startMatch(env, room, a, b);
    const token = (messagesTo(env, b.id, "room.state")[0]?.payload as { resumeToken: string }).resumeToken;
    const v = currentRound(env, b.id);
    env.time.advance(4000);
    room.disconnect(b.id);
    env.time.advance(3000);
    room.resume(b.id, token);
    const resent = messagesTo(env, b.id, "room.round");
    const last = resent[resent.length - 1]?.payload as unknown as RoundView & { resumed: boolean };
    expect(last.roundId).toBe(v.roundId);
    expect(last.resumed).toBe(true);
    expect(last.durationMs).toBe(v.durationMs - 4000); // kopukluk süresi sayılmaz
  });
});
