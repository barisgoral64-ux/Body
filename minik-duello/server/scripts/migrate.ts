import { Pool } from "pg";
import { runMigrations, seedStaticData } from "../src/infra/migrate.js";

/** Elle göç çalıştırma: DATABASE_URL=... npm run migrate */
const url = process.env.DATABASE_URL;
if (!url) throw new Error("DATABASE_URL gerekli");
const pool = new Pool({ connectionString: url });
const applied = await runMigrations(pool);
await seedStaticData(pool);
process.stdout.write(`Uygulanan göçler: ${applied.length ? applied.join(", ") : "(yok, güncel)"}\n`);
await pool.end();
