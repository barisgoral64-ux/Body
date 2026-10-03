import { randomUUID } from "node:crypto";

export interface Clock {
  now(): Date;
}
export const systemClock: Clock = { now: () => new Date() };

export type CancelFn = () => void;
export interface Scheduler {
  after(delayMs: number, task: () => void): CancelFn;
}
export const systemScheduler: Scheduler = {
  after(delayMs, task) {
    const handle = setTimeout(task, delayMs);
    return () => clearTimeout(handle);
  },
};

export type IdGenerator = () => string;
export const uuid: IdGenerator = () => randomUUID();

/** Test için: elle ilerletilen saat + zamanlayıcı. */
export class FakeTime implements Clock, Scheduler {
  private current: number;
  private seq = 0;
  private readonly tasks = new Map<number, { at: number; run: () => void }>();

  constructor(start = new Date("2026-01-01T00:00:00Z")) {
    this.current = start.getTime();
  }
  now(): Date {
    return new Date(this.current);
  }
  after(delayMs: number, task: () => void): CancelFn {
    const id = this.seq++;
    this.tasks.set(id, { at: this.current + delayMs, run: task });
    return () => void this.tasks.delete(id);
  }
  advance(ms: number): void {
    const target = this.current + ms;
    for (;;) {
      let nextId: number | null = null;
      let nextAt = Infinity;
      for (const [id, t] of this.tasks) {
        if (t.at <= target && t.at < nextAt) {
          nextAt = t.at;
          nextId = id;
        }
      }
      if (nextId === null) break;
      const t = this.tasks.get(nextId);
      this.tasks.delete(nextId);
      this.current = Math.max(this.current, nextAt);
      t?.run();
    }
    this.current = target;
  }
}
