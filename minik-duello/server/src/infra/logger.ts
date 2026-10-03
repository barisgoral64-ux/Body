export type LogLevel = "debug" | "info" | "warn" | "error";

const LEVEL_ORDER: Record<LogLevel, number> = { debug: 10, info: 20, warn: 30, error: 40 };

/** Log çıktısında asla yer almaması gereken anahtarlar. */
const REDACTED_KEYS = new Set(["token", "accessToken", "refreshToken", "pin", "pinHash", "password", "authorization"]);
const REDACTED_VALUE = "[gizli]";
const PLAYER_ID_VISIBLE_CHARS = 4;

export interface Logger {
  debug(message: string, fields?: Record<string, unknown>): void;
  info(message: string, fields?: Record<string, unknown>): void;
  warn(message: string, fields?: Record<string, unknown>): void;
  error(message: string, fields?: Record<string, unknown>): void;
  child(scope: string): Logger;
}

export type LogSink = (line: string) => void;

/** Oyuncu ID'sini loglarda maskeler (gizlilik): "abcd…". */
export function maskPlayerId(playerId: string): string {
  return playerId.length <= PLAYER_ID_VISIBLE_CHARS ? playerId : `${playerId.slice(0, PLAYER_ID_VISIBLE_CHARS)}…`;
}

function sanitize(fields: Record<string, unknown> | undefined): Record<string, unknown> | undefined {
  if (!fields) return undefined;
  const out: Record<string, unknown> = {};
  for (const [key, value] of Object.entries(fields)) {
    out[key] = REDACTED_KEYS.has(key) ? REDACTED_VALUE : value;
  }
  return out;
}

export function createLogger(
  minLevel: LogLevel,
  scope = "app",
  sink: LogSink = (line) => process.stdout.write(`${line}\n`),
): Logger {
  const write = (level: LogLevel, message: string, fields?: Record<string, unknown>): void => {
    if (LEVEL_ORDER[level] < LEVEL_ORDER[minLevel]) return;
    sink(JSON.stringify({ time: new Date().toISOString(), level, scope, message, ...sanitize(fields) }));
  };
  return {
    debug: (m, f) => write("debug", m, f),
    info: (m, f) => write("info", m, f),
    warn: (m, f) => write("warn", m, f),
    error: (m, f) => write("error", m, f),
    child: (childScope) => createLogger(minLevel, `${scope}.${childScope}`, sink),
  };
}
