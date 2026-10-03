using MinikDuello.Domain.Net;
using MinikDuello.Services.Managers;

namespace MinikDuello.UI
{
    /// <summary>Ekranlar arası paylaşılan seçimler (hangi dünya/bölüm, son sonuç, seçilen arkadaş).</summary>
    public sealed class NavContext
    {
        public int SelectedWorld = 1;
        public int SelectedLevel = 1;
        public LevelCompletion LastCompletion;
        public int LastScore;
        public int LastMistakes;

        public bool CoopSelected;
        public FriendDto SelectedFriend;
        /// <summary>Rakip bilgisi (davetten veya seçilen arkadaştan).</summary>
        public string OpponentName;
        public string OpponentCharacter = "panda";
        public string SelectedMode = GameModes.MixedMatch;

        /// <summary>Ebeveyn paneli açıkken doğrulanmış PIN (yalnızca bellekte, panel kapanınca silinir).</summary>
        public string ParentPin;

        public void ClearParent() => ParentPin = null;
    }
}
