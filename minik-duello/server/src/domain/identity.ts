import { IDENTITY } from "../config/constants.js";

export const ANIMAL_KEYS = ["PANDA", "TAVSAN", "KEDI", "KOPEK", "DINO", "TILKI", "KOALA", "PENGUEN"] as const;
const ANIMAL_USERNAME_PARTS = ["Panda", "Tavsan", "Kedi", "Kopek", "Dino", "Tilki", "Koala", "Penguen"] as const;
export const USERNAME_ADJECTIVES = ["Mutlu", "Neseli", "Cesur", "Sirin", "Hizli", "Akilli", "Tatli", "Minik"] as const;

export type RandomInt = (maxExclusive: number) => number;

const defaultRandom: RandomInt = (max) => Math.floor(Math.random() * max);

function pick<T>(items: readonly T[], random: RandomInt): T {
  const item = items[random(items.length)];
  if (item === undefined) throw new Error("Boş liste");
  return item;
}

/** Örn. "PANDA-4832". Alfabede 0/1 yok; okunması kolay. */
export function generateFriendCode(random: RandomInt = defaultRandom): string {
  const animal = pick(ANIMAL_KEYS, random);
  let digits = "";
  for (let i = 0; i < IDENTITY.friendCodeDigits; i += 1) {
    digits += pick([...IDENTITY.friendCodeAlphabet], random);
  }
  return `${animal}-${digits}`;
}

/** Örn. "MutluPanda27" — yalnızca beyaz listeden, kişisel bilgi içeremez. */
export function generateUsername(random: RandomInt = defaultRandom): string {
  const number = random(IDENTITY.usernameMaxNumber) + 1;
  return `${pick(USERNAME_ADJECTIVES, random)}${pick(ANIMAL_USERNAME_PARTS, random)}${number}`;
}

const FRIEND_CODE_PATTERN = new RegExp(
  `^(${ANIMAL_KEYS.join("|")})-[${IDENTITY.friendCodeAlphabet}]{${IDENTITY.friendCodeDigits}}$`,
);

/** Kullanıcı girdisini normalleştirir; geçersizse null. */
export function normalizeFriendCode(input: string): string | null {
  const candidate = input.trim().toUpperCase().replace(/Ğ/g, "G").replace(/İ/g, "I");
  return FRIEND_CODE_PATTERN.test(candidate) ? candidate : null;
}
