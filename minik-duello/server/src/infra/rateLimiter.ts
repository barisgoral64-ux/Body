import type { Clock } from "./runtime.js";

/** Sabit pencereli sayaç. Bellek şişmesini önlemek için eski anahtarlar periyodik temizlenir. */
export class RateLimiter {
  private readonly windows = new Map<string, { start: number; count: number }>();
  private static readonly MAX_KEYS = 50_000;

  constructor(
    private readonly limit: number,
    private readonly windowMs: number,
    private readonly clock: Clock,
  ) {}

  /** true: izin verildi. */
  hit(key: string): boolean {
    const now = this.clock.now().getTime();
    const w = this.windows.get(key);
    if (!w || now - w.start >= this.windowMs) {
      if (this.windows.size >= RateLimiter.MAX_KEYS) this.prune(now);
      this.windows.set(key, { start: now, count: 1 });
      return true;
    }
    w.count += 1;
    return w.count <= this.limit;
  }

  private prune(now: number): void {
    for (const [k, w] of this.windows) if (now - w.start >= this.windowMs) this.windows.delete(k);
  }
}
