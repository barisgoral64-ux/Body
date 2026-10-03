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
  httpRequestsPerMinute: 120,
  quickChatCooldownMs: 1_000,
} as const;
