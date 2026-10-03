/**
 * Tüm oyun ve güvenlik sabitleri burada toplanır (magic number yasak).
 * Değerler `as const` ile değişmez tutulur; ortama bağlı olanlar env.ts'tedir.
 */

export const PROTOCOL_VERSION = 1;

export const SCORING = {
  correctAnswer: 100,
  speedBonusMin: 10,
  speedBonusMax: 50,
  streakBonus: 20,
  wrongAnswer: 0,
  /** Bu süreden hızlı cevap insan dışı sayılır: hız bonusu verilmez. */
  minHumanResponseMs: 150,
} as const;

export const ROOM = {
  totalRounds: 5,
  roundDurationMsMin: 15_000,
  roundDurationMsMax: 30_000,
  countdownSeconds: 3,
  reconnectGraceMs: 15_000,
  coopTeamTargetStars: 10,
  playersPerRoom: 2,
} as const;

export const INVITE = {
  expiresInMs: 30_000,
  maxPendingPerSender: 1,
  resendCooldownMs: 5_000,
} as const;

export const FRIENDS = {
  requestExpiresInMs: 7 * 24 * 60 * 60 * 1000,
  rejectedCooldownMs: 24 * 60 * 60 * 1000,
  maxFriends: 50,
  maxRequestsPerDay: 10,
  /** Kod tahminiyle hesap bulmayı (numaralandırma) önlemek için, başarılı/başarısız tüm denemeler sayılır. */
  maxCodeAttemptsPerDay: 20,
} as const;

export const PRESENCE = {
  heartbeatIntervalMs: 15_000,
  ttlMs: 45_000,
} as const;

export const IDENTITY = {
  friendCodeDigits: 4,
  usernameMaxNumber: 99,
  /** Karışıklığı önlemek için 0/O ve 1/I içermeyen alfabe. */
  friendCodeAlphabet: "23456789",
} as const;

export const PARENT = {
  pinLength: 4,
  maxPinAttempts: 5,
  pinLockoutMs: 5 * 60 * 1000,
  /** Her ardışık kilitlenmede süre bu çarpanla büyür (üst sınırlı): kaba kuvvet denemesi pahalılaşır. */
  pinLockoutGrowth: 3,
  pinLockoutMaxMs: 24 * 60 * 60 * 1000,
  defaultDailyLimitMinutes: 0, // 0 = sınırsız
} as const;

export const LEVELS = {
  totalLevels: 100,
  levelsPerWorld: 10,
  totalWorlds: 10,
  /** Ardışık iki bölüm arasında izin verilen en büyük zorluk artışı (0-1 ölçeği). */
  maxDifficultyStep: 0.05,
} as const;

export const LIMITS = {
  wsMaxMessageBytes: 2_048,
  wsMessagesPerSecond: 20,
  wsConnectionsPerIp: 8,
  httpRequestsPerMinute: 120,
  quickChatCooldownMs: 1_000,
} as const;

export const AUTH = {
  accessTokenTtlSeconds: 15 * 60,
  refreshTokenTtlSeconds: 30 * 24 * 60 * 60,
  deviceIdMinLength: 16,
  deviceIdMaxLength: 128,
  wsAuthTimeoutMs: 5_000,
  scryptKeyLength: 32,
  saltBytes: 16,
} as const;

export const REWARDS = {
  matchWinnerStars: 3,
  matchWinnerCoins: 30,
  matchParticipantStars: 2,
  matchParticipantCoins: 20,
  /** Bağlantı koptuğunda veya erken bittiğinde herkese verilen asgari ödül (kimse cezalandırılmaz). */
  matchMinimumStars: 1,
  matchMinimumCoins: 10,
  coopSuccessStars: 3,
  coopSuccessCoins: 30,
  coopTryStars: 2,
  coopTryCoins: 20,
  levelBaseCoins: 10,
  levelCoinsPerStar: 5,
  maxStarsPerLevel: 3,
  starsPerPlayerLevel: 5,
} as const;

/** Toplam yıldız eşikleri ve açılan ödül kimlikleri. Oynayarak kazanılır, para gerektirmez. */
export const STAR_UNLOCKS: readonly { readonly stars: number; readonly rewardId: string }[] = [
  { stars: 5, rewardId: "hat_party" },
  { stars: 10, rewardId: "char_rabbit" },
  { stars: 20, rewardId: "glasses_round" },
  { stars: 30, rewardId: "char_cat" },
  { stars: 45, rewardId: "frame_gold" },
  { stars: 60, rewardId: "char_dog" },
  { stars: 80, rewardId: "backpack_rocket" },
  { stars: 100, rewardId: "char_dinosaur" },
  { stars: 130, rewardId: "effect_sparkle" },
  { stars: 160, rewardId: "char_fox" },
  { stars: 200, rewardId: "char_koala" },
  { stars: 250, rewardId: "char_penguin" },
];

export const STARTER_REWARDS: readonly string[] = ["char_panda"];

export const DAILY_REWARD_CYCLE: readonly { readonly coins: number; readonly rewardId: string | null }[] = [
  { coins: 50, rewardId: null },
  { coins: 0, rewardId: "sticker_star" },
  { coins: 100, rewardId: null },
  { coins: 0, rewardId: "sticker_heart" },
  { coins: 150, rewardId: null },
  { coins: 0, rewardId: "sticker_rainbow" },
  { coins: 200, rewardId: null },
];

export const SAVE = { maxBytes: 64 * 1024 } as const;

export const ROUND_DURATIONS_MS = {
  color: 15_000,
  shape: 15_000,
  number: 20_000,
  memory: 25_000,
  puzzle: 30_000,
  stars: 20_000,
} as const;

export const ROUND_TIMING = {
  betweenRoundsMs: 2_500,
  roomWaitTimeoutMs: 60_000,
  inviteResponsePurgeMs: 60_000,
  starsPerRound: 5,
  starFieldSize: 9,
  choicesPerRound: 4,
  memoryShowMs: 3_000,
  puzzlePieces: 4,
  coopPuzzleRounds: 3,
  coopStarsMaxRounds: 4,
  starCollectRounds: 3,
} as const;
