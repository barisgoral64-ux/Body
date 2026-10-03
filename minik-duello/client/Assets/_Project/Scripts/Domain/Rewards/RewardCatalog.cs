using System.Collections.Generic;

namespace MinikDuello.Domain.Rewards
{
    public enum RewardType
    {
        Character,
        Costume,
        Hat,
        Glasses,
        Shoes,
        Backpack,
        Effect,
        Frame,
        Sticker
    }

    public sealed class RewardDef
    {
        public string Id;
        public RewardType Type;
        public string Slot;
        /// <summary>Oyun içi coin fiyatı; null = yalnızca oynayarak açılır. Gerçek para yok.</summary>
        public int? ShopPrice;
        public string DisplayName;
    }

    /// <summary>server/src/domain/rewards.ts ile birebir aynı kimlikler (testle eşlik doğrulanır).</summary>
    public static class RewardCatalog
    {
        public static readonly string[] CharacterIds = { "panda", "rabbit", "cat", "dog", "dinosaur", "fox", "koala", "penguin" };
        public const string CharacterPrefix = "char_";

        public static readonly IReadOnlyList<RewardDef> All = new List<RewardDef>
        {
            D("char_panda", RewardType.Character, "character", null, "Panda"),
            D("char_rabbit", RewardType.Character, "character", null, "Tavşan"),
            D("char_cat", RewardType.Character, "character", null, "Kedi"),
            D("char_dog", RewardType.Character, "character", null, "Köpek"),
            D("char_dinosaur", RewardType.Character, "character", null, "Dinozor"),
            D("char_fox", RewardType.Character, "character", null, "Tilki"),
            D("char_koala", RewardType.Character, "character", null, "Koala"),
            D("char_penguin", RewardType.Character, "character", null, "Penguen"),
            D("hat_party", RewardType.Hat, "hat", null, "Parti Şapkası"),
            D("hat_crown", RewardType.Hat, "hat", 150, "Taç"),
            D("glasses_round", RewardType.Glasses, "glasses", null, "Yuvarlak Gözlük"),
            D("glasses_star", RewardType.Glasses, "glasses", 120, "Yıldız Gözlük"),
            D("outfit_hero", RewardType.Costume, "outfit", 200, "Kahraman Kıyafeti"),
            D("outfit_explorer", RewardType.Costume, "outfit", 200, "Kaşif Kıyafeti"),
            D("shoes_runner", RewardType.Shoes, "shoes", 100, "Koşu Ayakkabısı"),
            D("backpack_rocket", RewardType.Backpack, "backpack", null, "Roket Çanta"),
            D("backpack_wings", RewardType.Backpack, "backpack", 180, "Kanatlı Çanta"),
            D("effect_sparkle", RewardType.Effect, "effect", null, "Parıltı"),
            D("frame_gold", RewardType.Frame, "frame", null, "Altın Çerçeve"),
            D("sticker_star", RewardType.Sticker, "sticker", null, "Yıldız Sticker"),
            D("sticker_heart", RewardType.Sticker, "sticker", null, "Kalp Sticker"),
            D("sticker_rainbow", RewardType.Sticker, "sticker", null, "Gökkuşağı Sticker")
        };

        public static readonly string[] CosmeticSlots = { "hat", "glasses", "outfit", "shoes", "backpack", "effect" };

        public static RewardDef Find(string id)
        {
            foreach (RewardDef def in All)
            {
                if (def.Id == id) return def;
            }
            return null;
        }

        public static string CharacterName(string characterId)
        {
            RewardDef def = Find(CharacterPrefix + characterId);
            return def != null ? def.DisplayName : characterId;
        }

        private static RewardDef D(string id, RewardType type, string slot, int? price, string name) =>
            new RewardDef { Id = id, Type = type, Slot = slot, ShopPrice = price, DisplayName = name };
    }
}
