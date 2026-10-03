import { createHmac, timingSafeEqual } from "node:crypto";
import { ErrorCode, fail, ok, type Result } from "../shared/result.js";
import type { Clock } from "../infra/runtime.js";

export type TokenType = "access" | "refresh";

interface TokenClaims {
  sub: string;
  typ: TokenType;
  iat: number;
  exp: number;
}

const HEADER = { alg: "HS256", typ: "JWT" } as const;
const SEGMENTS = 3;
const MS_PER_SECOND = 1000;

const b64 = (input: Buffer | string): string => Buffer.from(input).toString("base64url");

/** Bağımlılıksız HS256 JWT. Algoritma sabittir ("none" ve algoritma karıştırma saldırıları kapalı). */
export class TokenService {
  constructor(
    private readonly secret: string,
    private readonly clock: Clock,
  ) {}

  sign(playerId: string, type: TokenType, ttlSeconds: number): string {
    const iat = Math.floor(this.clock.now().getTime() / MS_PER_SECOND);
    const claims: TokenClaims = { sub: playerId, typ: type, iat, exp: iat + ttlSeconds };
    const body = `${b64(JSON.stringify(HEADER))}.${b64(JSON.stringify(claims))}`;
    return `${body}.${this.signature(body)}`;
  }

  verify(token: string, expected: TokenType): Result<string> {
    const parts = token.split(".");
    if (parts.length !== SEGMENTS) return fail(ErrorCode.Unauthorized, "Geçersiz token");
    const [h, c, s] = parts as [string, string, string];

    const given = Buffer.from(s, "base64url");
    const wanted = Buffer.from(this.signature(`${h}.${c}`), "base64url");
    if (given.length !== wanted.length || !timingSafeEqual(given, wanted)) {
      return fail(ErrorCode.Unauthorized, "Geçersiz imza");
    }

    try {
      const header = JSON.parse(Buffer.from(h, "base64url").toString("utf8")) as { alg?: string };
      if (header.alg !== HEADER.alg) return fail(ErrorCode.Unauthorized, "Geçersiz algoritma");
      const claims = JSON.parse(Buffer.from(c, "base64url").toString("utf8")) as Partial<TokenClaims>;
      if (claims.typ !== expected || typeof claims.sub !== "string" || typeof claims.exp !== "number") {
        return fail(ErrorCode.Unauthorized, "Geçersiz token türü");
      }
      if (claims.exp * MS_PER_SECOND <= this.clock.now().getTime()) {
        return fail(ErrorCode.Unauthorized, "Token süresi doldu");
      }
      return ok(claims.sub);
    } catch {
      return fail(ErrorCode.Unauthorized, "Bozuk token");
    }
  }

  private signature(body: string): string {
    return createHmac("sha256", this.secret).update(body).digest("base64url");
  }
}
