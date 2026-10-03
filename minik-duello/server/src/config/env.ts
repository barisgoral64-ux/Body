import { z } from "zod";

const MIN_SECRET_LENGTH = 32;
const DEV_FALLBACK_SECRET = "dev-only-secret-do-not-use-in-production-0000";
const DEV_FALLBACK_DEVICE_SECRET = "dev-only-device-secret-do-not-use-in-production";

const envSchema = z.object({
  NODE_ENV: z.enum(["development", "production", "test"]).default("development"),
  PORT: z.coerce.number().int().min(1).max(65535).default(3000),
  HOST: z.string().default("0.0.0.0"),
  LOG_LEVEL: z.enum(["debug", "info", "warn", "error"]).optional(),
  JWT_SECRET: z.string().optional(),
  DEVICE_SECRET: z.string().optional(),
  TRUST_PROXY: z.enum(["true", "false"]).default("false"),
  DATABASE_URL: z.string().optional(),
});

export type AppEnvironment = "development" | "production" | "test";

export interface AppConfig {
  readonly environment: AppEnvironment;
  readonly isProduction: boolean;
  readonly port: number;
  readonly host: string;
  readonly logLevel: "debug" | "info" | "warn" | "error";
  readonly jwtSecret: string;
  /** Cihaz kimliği özeti için AYRI sır: JWT sırrı döndürülse de hesaplar yetim kalmaz. */
  readonly deviceSecret: string;
  /** Yük dengeleyici arkasında gerçek istemci IP'sini X-Forwarded-For'dan al. */
  readonly trustProxy: boolean;
  readonly databaseUrl: string | null;
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
  if (isProduction && (env.DEVICE_SECRET ?? "").length < MIN_SECRET_LENGTH) {
    throw new Error(`Üretimde DEVICE_SECRET en az ${MIN_SECRET_LENGTH} karakter olmalıdır.`);
  }
  if (isProduction && env.DEVICE_SECRET === env.JWT_SECRET) {
    throw new Error("DEVICE_SECRET ve JWT_SECRET farklı olmalıdır.");
  }
  if (isProduction && !env.DATABASE_URL) throw new Error("Üretimde DATABASE_URL zorunludur.");

  return Object.freeze({
    environment: env.NODE_ENV,
    isProduction,
    port: env.PORT,
    host: env.HOST,
    logLevel: env.LOG_LEVEL ?? (isProduction ? "info" : "debug"),
    jwtSecret: env.JWT_SECRET && env.JWT_SECRET.length > 0 ? env.JWT_SECRET : DEV_FALLBACK_SECRET,
    deviceSecret: env.DEVICE_SECRET && env.DEVICE_SECRET.length > 0 ? env.DEVICE_SECRET : DEV_FALLBACK_DEVICE_SECRET,
    trustProxy: env.TRUST_PROXY === "true",
    databaseUrl: env.DATABASE_URL && env.DATABASE_URL.length > 0 ? env.DATABASE_URL : null,
  });
}
