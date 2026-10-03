import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { buildLevelCatalog } from "../src/domain/levels.js";

/** Sunucu kataloğunu Unity Resources klasörüne yazar; istemci ile sunucu aynı veriyi kullanır. */
const target = join(dirname(fileURLToPath(import.meta.url)), "..", "..", "client", "Assets", "_Project", "Resources", "levels.json");
mkdirSync(dirname(target), { recursive: true });
writeFileSync(target, `${JSON.stringify({ levels: buildLevelCatalog() }, null, 2)}\n`);
process.stdout.write(`levels.json yazıldı: ${target}\n`);
