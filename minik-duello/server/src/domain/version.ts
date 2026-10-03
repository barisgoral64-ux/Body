/** "1.2.3" veya "1.2.3-test" → [1,2,3]. Geçersizse null. Ön ek (-test) karşılaştırmada yok sayılır. */
export function parseVersion(input: string): readonly [number, number, number] | null {
  const match = /^(\d{1,4})\.(\d{1,4})\.(\d{1,4})(?:[-+][0-9A-Za-z.-]*)?$/.exec(input.trim());
  if (!match) return null;
  return [Number(match[1]), Number(match[2]), Number(match[3])];
}

/** a<b → -1, a==b → 0, a>b → 1. Geçersiz sürüm → null. */
export function compareVersions(a: string, b: string): -1 | 0 | 1 | null {
  const pa = parseVersion(a);
  const pb = parseVersion(b);
  if (!pa || !pb) return null;
  for (let i = 0; i < 3; i += 1) {
    const x = pa[i] as number;
    const y = pb[i] as number;
    if (x !== y) return x < y ? -1 : 1;
  }
  return 0;
}
