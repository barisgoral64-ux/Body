/** Serbest metin yok: yalnızca bu kapalı küme gönderilebilir. */
export const QUICK_CHAT_IDS = ["hello", "great", "congrats", "playAgain", "nice", "thanks"] as const;
export type QuickChatId = (typeof QUICK_CHAT_IDS)[number];

export const QUICK_CHAT_EMOJI: Readonly<Record<QuickChatId, string>> = {
  hello: "👋",
  great: "🎉",
  congrats: "👏",
  playAgain: "😊",
  nice: "⭐",
  thanks: "❤️",
};

export function isQuickChatId(value: unknown): value is QuickChatId {
  return typeof value === "string" && (QUICK_CHAT_IDS as readonly string[]).includes(value);
}
