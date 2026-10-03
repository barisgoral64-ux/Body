using System.Collections.Generic;
using MinikDuello.Domain.Rewards;
using MinikDuello.Services.Save;

namespace MinikDuello.Services.Managers
{
    /// <summary>Karakter ve kuşanılmış kozmetikler (yerel önbellekten okunur).</summary>
    public sealed class CharacterManager
    {
        private readonly SaveManager save;

        public CharacterManager(SaveManager save)
        {
            this.save = save;
        }

        public string CurrentCharacter => save.Data.CachedCharacter;

        public IEnumerable<string> OwnedCharacters()
        {
            foreach (string id in RewardCatalog.CharacterIds)
            {
                if (save.Data.OwnedRewards.Contains(RewardCatalog.CharacterPrefix + id) || id == "panda") yield return id;
            }
        }

        public bool IsCharacterOwned(string characterId)
        {
            foreach (string id in OwnedCharacters())
            {
                if (id == characterId) return true;
            }
            return false;
        }

        /// <summary>Slot → kuşanılmış öğe kimliği.</summary>
        public Dictionary<string, string> EquippedBySlot()
        {
            var map = new Dictionary<string, string>();
            foreach (string rewardId in save.Data.EquippedRewards)
            {
                RewardDef def = RewardCatalog.Find(rewardId);
                if (def != null && def.Type != RewardType.Character) map[def.Slot] = rewardId;
            }
            return map;
        }
    }
}
