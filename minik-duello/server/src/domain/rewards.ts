export type RewardType = "character" | "costume" | "hat" | "glasses" | "shoes" | "backpack" | "effect" | "frame" | "sticker";

export interface RewardDef {
  readonly id: string;
  readonly type: RewardType;
  /** Aynı slottaki öğelerden yalnızca biri takılabilir. */
  readonly slot: string;
  /** Oyun içi coin fiyatı; null = yalnızca oynayarak/yıldızla açılır. Gerçek para yok. */
  readonly shopPrice: number | null;
}

const def = (id: string, type: RewardType, slot: string, shopPrice: number | null = null): RewardDef => ({
  id, type, slot, shopPrice,
});

export const REWARD_CATALOG: readonly RewardDef[] = [
  def("char_panda", "character", "character"),
  def("char_rabbit", "character", "character"),
  def("char_cat", "character", "character"),
  def("char_dog", "character", "character"),
  def("char_dinosaur", "character", "character"),
  def("char_fox", "character", "character"),
  def("char_koala", "character", "character"),
  def("char_penguin", "character", "character"),
  def("hat_party", "hat", "hat"),
  def("hat_crown", "hat", "hat", 150),
  def("glasses_round", "glasses", "glasses"),
  def("glasses_star", "glasses", "glasses", 120),
  def("outfit_hero", "costume", "outfit", 200),
  def("outfit_explorer", "costume", "outfit", 200),
  def("shoes_runner", "shoes", "shoes", 100),
  def("backpack_rocket", "backpack", "backpack"),
  def("backpack_wings", "backpack", "backpack", 180),
  def("effect_sparkle", "effect", "effect"),
  def("frame_gold", "frame", "frame"),
  def("sticker_star", "sticker", "sticker"),
  def("sticker_heart", "sticker", "sticker"),
  def("sticker_rainbow", "sticker", "sticker"),
];

const BY_ID = new Map(REWARD_CATALOG.map((r) => [r.id, r]));
export const rewardById = (id: string): RewardDef | undefined => BY_ID.get(id);

export const CHARACTER_REWARD_PREFIX = "char_";
