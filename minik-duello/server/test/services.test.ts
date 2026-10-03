import { describe, expect, it } from "vitest";
import { DAILY_REWARD_CYCLE, FRIENDS, PARENT, REWARDS } from "../src/config/constants.js";
import { TokenService } from "../src/security/tokens.js";
import { ErrorCode } from "../src/shared/result.js";
import { createEnv, enableSocial, makeFriends, newPlayer, PIN } from "./helpers.js";

const DAY = 24 * 60 * 60 * 1000;

describe("token ve kimlik", () => {
  it("aynı cihaz aynı hesaba döner, token doğrulanır", async () => {
    const env = await createEnv();
    const a = await env.c.auth.registerAnonymous("same-device-id-0123456789");
    const b = await env.c.auth.registerAnonymous("same-device-id-0123456789");
    expect(a.ok && b.ok && a.value.player.playerId === b.value.player.playerId).toBe(true);
    expect(a.ok && env.c.auth.verifyAccessToken(a.value.tokens.accessToken).ok).toBe(true);
  });
  it("kısa cihaz kimliği reddedilir", async () => {
    const r = await (await createEnv()).c.auth.registerAnonymous("kisa");
    expect(r.ok).toBe(false);
  });
  it("refresh token access olarak kullanılamaz, bozuk imza reddedilir, süre dolar", async () => {
    const env = await createEnv();
    const r = await env.c.auth.registerAnonymous("device-for-token-tests-01");
    if (!r.ok) throw new Error("x");
    expect(env.c.auth.verifyAccessToken(r.value.tokens.refreshToken).ok).toBe(false);
    const [h, p, s] = r.value.tokens.accessToken.split(".") as [string, string, string];
    expect(env.c.auth.verifyAccessToken(`${h}.${p}.${s.slice(0, -2)}xx`).ok).toBe(false);
    expect(env.c.auth.verifyAccessToken("a.b").ok).toBe(false);
    env.time.advance(DAY);
    expect(env.c.auth.verifyAccessToken(r.value.tokens.accessToken).ok).toBe(false);
    expect((await env.c.auth.refresh(r.value.tokens.refreshToken)).ok).toBe(true);
  });
  it("alg=none ile üretilmiş token reddedilir", async () => {
    const env = await createEnv();
    const svc = new TokenService("secret-secret-secret-secret-secret", env.time);
    const header = Buffer.from(JSON.stringify({ alg: "none", typ: "JWT" })).toString("base64url");
    const claims = Buffer.from(JSON.stringify({ sub: "x", typ: "access", iat: 0, exp: 9999999999 })).toString("base64url");
    expect(svc.verify(`${header}.${claims}.`, "access").ok).toBe(false);
  });
  it("yeni hesap güvenli varsayılanlarla ve başlangıç karakteriyle açılır", async () => {
    const env = await createEnv();
    const p = await newPlayer(env);
    const s = await env.c.parent.getSettings(p.id);
    expect(s.ok && [s.value.friendsEnabled, s.value.multiplayerEnabled, s.value.hasPin]).toEqual([false, false, false]);
    expect((await env.c.rewards.listInventory(p.id)).map((i) => i.rewardId)).toContain("char_panda");
    expect(p.code).toMatch(/^[A-Z]+-[2-9]{4}$/);
  });
});

describe("ebeveyn kontrolleri", () => {
  it("PIN yokken sosyal özellik açılamaz", async () => {
    const env = await createEnv();
    const p = await newPlayer(env);
    const r = await env.c.parent.updateSettings(p.id, { friendsEnabled: true });
    expect(r.ok === false && r.error.code).toBe(ErrorCode.Forbidden);
  });
  it("yanlış PIN ayarı değiştiremez, 5 yanlışta kilitlenir, süre sonra açılır", async () => {
    const env = await createEnv();
    const p = await newPlayer(env);
    await env.c.parent.setPin(p.id, PIN);
    for (let i = 0; i < PARENT.maxPinAttempts; i += 1) {
      await env.c.parent.updateSettings(p.id, { friendsEnabled: true }, "0000");
    }
    const locked = await env.c.parent.updateSettings(p.id, { friendsEnabled: true }, PIN);
    expect(locked.ok === false && locked.error.code).toBe(ErrorCode.RateLimited);
    env.time.advance(PARENT.pinLockoutMs + 1);
    const ok = await env.c.parent.updateSettings(p.id, { friendsEnabled: true }, PIN);
    expect(ok.ok).toBe(true);
  });
  it("PIN değişikliği mevcut PIN ister; geçersiz format reddedilir", async () => {
    const env = await createEnv();
    const p = await newPlayer(env);
    expect((await env.c.parent.setPin(p.id, "12")).ok).toBe(false);
    await env.c.parent.setPin(p.id, PIN);
    expect((await env.c.parent.setPin(p.id, "9999")).ok).toBe(false);
    expect((await env.c.parent.setPin(p.id, "9999", PIN)).ok).toBe(true);
  });
  it("geçersiz süre sınırı reddedilir", async () => {
    const env = await createEnv();
    const p = await newPlayer(env);
    expect((await env.c.parent.updateSettings(p.id, { dailyLimitMinutes: -5 })).ok).toBe(false);
    expect((await env.c.parent.updateSettings(p.id, { dailyLimitMinutes: 45 })).ok).toBe(true);
  });
});

describe("arkadaşlık", () => {
  it("karşılıklı arkadaşlık: istek → kabul → iki taraf da listede", async () => {
    const env = await createEnv();
    const a = await newPlayer(env);
    const b = await newPlayer(env);
    await makeFriends(env, a, b);
    const la = await env.c.friends.listFriends(a.id);
    const lb = await env.c.friends.listFriends(b.id);
    expect(la.ok && la.value.map((f) => f.playerId)).toEqual([b.id]);
    expect(lb.ok && lb.value.map((f) => f.playerId)).toEqual([a.id]);
  });
  it("istek tek taraflı arkadaşlık yaratmaz", async () => {
    const env = await createEnv();
    const a = await newPlayer(env);
    const b = await newPlayer(env);
    await enableSocial(env, a.id);
    await enableSocial(env, b.id);
    await env.c.friendRequests.send(a.id, b.code);
    expect(await env.c.friends.areFriends(a.id, b.id)).toBe(false);
  });
  it("ebeveyn izni kapalıyken istek gönderilemez", async () => {
    const env = await createEnv();
    const a = await newPlayer(env);
    const b = await newPlayer(env);
    const r = await env.c.friendRequests.send(a.id, b.code);
    expect(r.ok === false && r.error.code).toBe(ErrorCode.FriendsDisabled);
  });
  it("alıcı tarafı nötr: kod yok / alıcı kapalı / engelli aynı başarılı yanıt, istek oluşmaz", async () => {
    const env = await createEnv();
    const a = await newPlayer(env);
    const closed = await newPlayer(env); // sosyal kapalı
    await enableSocial(env, a.id);
    const unknown = await env.c.friendRequests.send(a.id, "PANDA-2222");
    const toClosed = await env.c.friendRequests.send(a.id, closed.code);
    expect(unknown.ok).toBe(true);
    expect(toClosed.ok).toBe(true);
    expect(await env.store.listPendingIncoming(closed.id, env.time.now())).toHaveLength(0);
  });
  it("geçersiz kod ve kendi kodu reddedilir", async () => {
    const env = await createEnv();
    const a = await newPlayer(env);
    await enableSocial(env, a.id);
    expect((await env.c.friendRequests.send(a.id, "x' OR 1=1")).ok).toBe(false);
    expect((await env.c.friendRequests.send(a.id, a.code)).ok).toBe(false);
  });
  it("başkası başkasının isteğini kabul edemez", async () => {
    const env = await createEnv();
    const [a, b, c] = [await newPlayer(env), await newPlayer(env), await newPlayer(env)];
    await enableSocial(env, a.id);
    await enableSocial(env, b.id);
    await enableSocial(env, c.id);
    await env.c.friendRequests.send(a.id, b.code);
    const incoming = await env.c.friendRequests.listIncoming(b.id);
    const id = incoming.ok ? incoming.value[0]?.requestId ?? "" : "";
    const r = await env.c.friendRequests.respond(c.id, id, true);
    expect(r.ok === false && r.error.code).toBe(ErrorCode.NotFound);
    expect(await env.c.friends.areFriends(a.id, c.id)).toBe(false);
  });
  it("süresi dolan istek kabul edilemez", async () => {
    const env = await createEnv();
    const a = await newPlayer(env);
    const b = await newPlayer(env);
    await enableSocial(env, a.id);
    await enableSocial(env, b.id);
    await env.c.friendRequests.send(a.id, b.code);
    const incoming = await env.c.friendRequests.listIncoming(b.id);
    const id = incoming.ok ? incoming.value[0]?.requestId ?? "" : "";
    env.time.advance(FRIENDS.requestExpiresInMs + 1);
    const r = await env.c.friendRequests.respond(b.id, id, true);
    expect(r.ok === false && r.error.code).toBe(ErrorCode.RequestExpired);
  });
  it("reddedilen isteğe 24 saat içinde tekrar istek oluşmaz; sonra olur", async () => {
    const env = await createEnv();
    const a = await newPlayer(env);
    const b = await newPlayer(env);
    await enableSocial(env, a.id);
    await enableSocial(env, b.id);
    await env.c.friendRequests.send(a.id, b.code);
    const first = await env.c.friendRequests.listIncoming(b.id);
    await env.c.friendRequests.respond(b.id, first.ok ? first.value[0]?.requestId ?? "" : "", false);
    await env.c.friendRequests.send(a.id, b.code);
    expect(await env.store.listPendingIncoming(b.id, env.time.now())).toHaveLength(0);
    env.time.advance(FRIENDS.rejectedCooldownMs + 1);
    await env.c.friendRequests.send(a.id, b.code);
    expect(await env.store.listPendingIncoming(b.id, env.time.now())).toHaveLength(1);
  });
  it("kod tahmini (numaralandırma) günlük deneme sınırıyla durdurulur; gün sonra yenilenir", async () => {
    const env = await createEnv();
    const a = await newPlayer(env);
    await enableSocial(env, a.id);
    let last = await env.c.friendRequests.send(a.id, "PANDA-2222"); // var olmayan kod da sayılır
    for (let i = 0; i < FRIENDS.maxCodeAttemptsPerDay; i += 1) last = await env.c.friendRequests.send(a.id, "PANDA-2222");
    expect(last.ok === false && last.error.code).toBe(ErrorCode.RateLimited);
    env.time.advance(DAY + 1);
    expect((await env.c.friendRequests.send(a.id, "PANDA-2222")).ok).toBe(true);
  });
  it("gerçek isteklerin günlük sınırı", async () => {
    const env = await createEnv();
    const a = await newPlayer(env);
    await enableSocial(env, a.id);
    let last: Awaited<ReturnType<typeof env.c.friendRequests.send>> = { ok: true, value: undefined };
    for (let i = 0; i < FRIENDS.maxRequestsPerDay + 1; i += 1) {
      const target = await newPlayer(env);
      await enableSocial(env, target.id);
      last = await env.c.friendRequests.send(a.id, target.code);
    }
    expect(last.ok === false && last.error.code).toBe(ErrorCode.RateLimited);
  });
  it("engelleme arkadaşlığı ve bekleyen istekleri kaldırır; engelli yeni istek gönderemez", async () => {
    const env = await createEnv();
    const a = await newPlayer(env);
    const b = await newPlayer(env);
    await makeFriends(env, a, b);
    expect((await env.c.parent.blockPlayer(a.id, b.id, "0000")).ok).toBe(false);
    expect((await env.c.parent.blockPlayer(a.id, b.id, PIN)).ok).toBe(true);
    expect(await env.c.friends.areFriends(a.id, b.id)).toBe(false);
    await env.c.friendRequests.send(b.id, a.code);
    expect(await env.store.listPendingIncoming(a.id, env.time.now())).toHaveLength(0);
    expect(await env.c.parent.isBlocked(b.id, a.id)).toBe(true);
  });
  it("ebeveyn PIN ile arkadaş kaldırır", async () => {
    const env = await createEnv();
    const a = await newPlayer(env);
    const b = await newPlayer(env);
    await makeFriends(env, a, b);
    expect((await env.c.parent.removeFriend(a.id, b.id, "0000")).ok).toBe(false);
    expect((await env.c.parent.removeFriend(a.id, b.id, PIN)).ok).toBe(true);
    expect(await env.c.friends.areFriends(a.id, b.id)).toBe(false);
  });
});

describe("presence gizliliği", () => {
  it("izleyici yalnızca arkadaş + izin açıkken gerçek durumu görür", async () => {
    const env = await createEnv();
    const a = await newPlayer(env);
    const b = await newPlayer(env);
    const stranger = await newPlayer(env);
    await makeFriends(env, a, b);
    env.c.presence.set(b.id, "online");
    expect(await env.c.presence.visibleTo(a.id, b.id)).toBe("online");
    expect(await env.c.presence.visibleTo(stranger.id, b.id)).toBe("offline");
    await env.c.parent.updateSettings(b.id, { onlineStatusVisible: false }, PIN);
    expect(await env.c.presence.visibleTo(a.id, b.id)).toBe("offline");
  });
  it("heartbeat kesilince offline olur", async () => {
    const env = await createEnv();
    const a = await newPlayer(env);
    env.c.presence.set(a.id, "online");
    env.time.advance(60_000);
    expect(env.c.presence.actual(a.id)).toBe("offline");
  });
});

describe("ödüller ve bölüm sonucu", () => {
  const goodDuration = 20_000;
  it("günlük ödül günde bir kez; kaçırılan gün seriyi bozmaz", async () => {
    const env = await createEnv();
    const p = await newPlayer(env);
    const d1 = await env.c.rewards.claimDaily(p.id);
    expect(d1.ok && d1.value.coins).toBe(DAILY_REWARD_CYCLE[0]?.coins);
    expect((await env.c.rewards.claimDaily(p.id)).ok).toBe(false);
    env.time.advance(5 * DAY); // 4 gün kaçırıldı
    const d2 = await env.c.rewards.claimDaily(p.id);
    expect(d2.ok && d2.value.dayIndex).toBe(1); // kaldığı yerden devam
    expect(d2.ok && d2.value.rewardId).toBe("sticker_star");
  });
  it("bölüm sonucu: yıldız/coin sunucuda hesaplanır, tekrar oynama coin çiftçiliği yapmaz", async () => {
    const env = await createEnv();
    const p = await newPlayer(env);
    const level = env.c.levels.getCatalog()[0];
    if (!level) throw new Error("x");
    const first = await env.c.levels.submitResult(p.id, 1, level.targetScore, goodDuration);
    expect(first.ok && first.value.stars).toBe(REWARDS.maxStarsPerLevel);
    expect(first.ok && first.value.coinsGained).toBe(level.reward.coins + 3 * REWARDS.levelCoinsPerStar);
    const again = await env.c.levels.submitResult(p.id, 1, level.targetScore, goodDuration);
    expect(again.ok && again.value.coinsGained).toBe(0);
    expect(again.ok && again.value.starsGained).toBe(0);
    const me = await env.c.players.getSelf(p.id);
    expect(me.ok && me.value.totalStars).toBe(3);
  });
  it("kilitli bölüm, hileli skor ve insan altı süre reddedilir", async () => {
    const env = await createEnv();
    const p = await newPlayer(env);
    expect((await env.c.levels.submitResult(p.id, 2, 100, goodDuration)).ok).toBe(false);
    expect((await env.c.levels.submitResult(p.id, 1, 99999, goodDuration)).ok).toBe(false);
    expect((await env.c.levels.submitResult(p.id, 1, 500, 100)).ok).toBe(false);
    expect((await env.c.levels.submitResult(p.id, 1, -5, goodDuration)).ok).toBe(false);
    expect((await env.c.levels.submitResult(p.id, 999, 100, goodDuration)).ok).toBe(false);
  });
  it("0 yıldızlı sonuç sonraki bölümü açmaz; 1+ yıldız açar", async () => {
    const env = await createEnv();
    const p = await newPlayer(env);
    await env.c.levels.submitResult(p.id, 1, 0, goodDuration);
    expect((await env.c.levels.getProgress(p.id)).levels[1]?.unlocked).toBe(false);
    await env.c.levels.submitResult(p.id, 1, 300, goodDuration);
    expect((await env.c.levels.getProgress(p.id)).levels[1]?.unlocked).toBe(true);
    expect((await env.c.levels.submitResult(p.id, 2, 100, goodDuration)).ok).toBe(true);
  });
  it("yıldız eşiği kozmetik açar ve seviye yükselir", async () => {
    const env = await createEnv();
    const p = await newPlayer(env);
    const g = await env.c.rewards.grant(p.id, 10, 0, "test", null);
    expect(g.newRewards).toEqual(expect.arrayContaining(["hat_party", "char_rabbit"]));
    const me = await env.c.players.getSelf(p.id);
    expect(me.ok && me.value.level).toBe(1 + Math.floor(10 / REWARDS.starsPerPlayerLevel));
  });
  it("mağaza: yeterli coin gerekir, çift satın alma yok, takma slot dışlayıcı", async () => {
    const env = await createEnv();
    const p = await newPlayer(env);
    expect((await env.c.rewards.buy(p.id, "hat_crown")).ok).toBe(false);
    await env.c.rewards.grant(p.id, 0, 500, "test", null);
    const bought = await env.c.rewards.buy(p.id, "hat_crown");
    expect(bought.ok && bought.value.coins).toBe(350);
    expect((await env.c.rewards.buy(p.id, "hat_crown")).ok).toBe(false);
    expect((await env.c.rewards.buy(p.id, "char_dog")).ok).toBe(false); // mağazada yok
    await env.c.rewards.grant(p.id, 5, 0, "test", null); // hat_party açılır
    await env.c.rewards.equip(p.id, "hat_party", true);
    await env.c.rewards.equip(p.id, "hat_crown", true);
    const inv = await env.c.rewards.listInventory(p.id);
    expect(inv.find((i) => i.rewardId === "hat_party")?.equipped).toBe(false);
    expect(inv.find((i) => i.rewardId === "hat_crown")?.equipped).toBe(true);
    expect((await env.c.rewards.equip(p.id, "outfit_hero", true)).ok).toBe(false); // sahip değil
  });
  it("haftalık skor yalnızca arkadaşlar arası ve sahibini içerir", async () => {
    const env = await createEnv();
    const a = await newPlayer(env);
    const b = await newPlayer(env);
    const stranger = await newPlayer(env);
    await makeFriends(env, a, b);
    await env.c.rewards.grant(a.id, 4, 0, "t", null);
    await env.c.rewards.grant(b.id, 7, 0, "t", null);
    await env.c.rewards.grant(stranger.id, 99, 0, "t", null);
    const lb = await env.c.leaderboard.weeklyAmongFriends(a.id);
    expect(lb.ok && lb.value.map((e) => [e.playerId, e.stars])).toEqual([[b.id, 7], [a.id, 4]]);
  });
  it("bulut kaydı boyut sınırı", async () => {
    const env = await createEnv();
    const p = await newPlayer(env);
    expect((await env.c.saves.save(p.id, { music: 0.5 })).ok).toBe(true);
    expect((await env.c.saves.save(p.id, { blob: "x".repeat(70_000) })).ok).toBe(false);
  });
});
