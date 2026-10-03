import { z } from "zod";

const MIN_SECRET_LENGTH = 32;
const DEV_FALLBACK_SECRET = "dev-only-secret-do-not-use-in-production-0000";

const envSchema = z.object({
  NODE_ENV: z.enum(["development", "production", "test"]).default("development"),
  PORT: z.coerce.number().int().min(1).max(65535).default(3000),
  HOST: z.string().default("0.0.0.0"),
  LOG_LEVEL: z.enum(["debug", "info", "warn", "error"]).optional(),
  JWT_SECRET: z.string().optional(),
  DATABASE_URL: z.string().optional(),
  REDIS_URL: z.string().default("redis://localhost:6379"),
});

export type AppEnvironment = "development" | "production" | "test";

export interface AppConfig {
  readonly environment: AppEnvironment;
  readonly isProduction: boolean;
  readonly port: number;
  readonly host: string;
  readonly logLevel: "debug" | "info" | "warn" | "error";
  readonly jwtSecret: string;
  readonly databaseUrl: string | null;
  readonly redisUrl: string;
}

/**
 * Ortam değişkenlerini doğrular ve değişmez bir config döndürür.
 * Üretimde eksik/zayıf JWT_SECRET uygulamanın açılmasını engeller.
 */
export function loadConfig(source: Record<string, string | undefined> = process.env): AppConfig {
  const parsed = envSchema.safeParse(source);
  if (!parsed.success) {
    const details = parsed.error.issues.map((i) => `${i.path.join(".")}: ${i.message}`).join("; ");
    throw new Error(`Geçersiz ortam yapılandırması: ${details}`);
  }
  const env = parsed.data;
  const isProduction = env.NODE_ENV === "production";

  if (isProduction && (env.JWT_SECRET ?? "").length < MIN_SECRET_LENGTH) {
    throw new Error(`Üretimde JWT_SECRET en az ${MIN_SECRET_LENGTH} karakter olmalıdır.`);
  }
  if (isProduction && !env.DATABASE_URL) throw new Error("Üretimde DATABASE_URL zorunludur.");

  return Object.freeze({
    environment: env.NODE_ENV,
    isProduction,
    port: env.PORT,
    host: env.HOST,
    logLevel: env.LOG_LEVEL ?? (isProduction ? "info" : "debug"),
    jwtSecret: env.JWT_SECRET && env.JWT_SECRET.length > 0 ? env.JWT_SECRET : DEV_FALLBACK_SECRET,
    databaseUrl: env.DATABASE_URL && env.DATABASE_URL.length > 0 ? env.DATABASE_URL : null,
    redisUrl: env.REDIS_URL,
  });
}
