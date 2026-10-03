using MinikDuello.Core;
using MinikDuello.Domain.Rewards;
using MinikDuello.Services.Managers;
using UnityEngine;

namespace MinikDuello.UI
{
    /// <summary>Profil: avatar, takma ad (sistem üretimli), seviye, yıldız, koleksiyon. Gerçek isim/foto yoktur.</summary>
    public sealed class ProfileScreen : ScreenBase
    {
        private RectTransform content;

        protected override void Build()
        {
            BuildHeader("PROFİLİM");
            content = UIFactory.CreateColumn(transform, UITheme.Spacing);
        }

        protected override void OnShown()
        {
            Refresh();
            RunAsync(async () =>
            {
                await ServiceLocator.Get<PlayerManager>().RefreshAsync();
                await ServiceLocator.Get<RewardManager>().RefreshInventoryAsync();
                if (IsAlive) Refresh();
            });
        }

        private void Refresh()
        {
            UIFactory.ClearChildren(content);
            var player = ServiceLocator.Get<PlayerManager>();
            var characters = ServiceLocator.Get<CharacterManager>();
            var rewards = ServiceLocator.Get<RewardManager>();
            var levels = ServiceLocator.Get<LevelManager>();

            RectTransform avatarRow = UIFactory.CreateRow(content, 0f, 300f);
            UIKit.Avatar(avatarRow, player.Character, characters.EquippedBySlot(), 280f);
            UIFactory.CreateLabel(content, player.Username, UITheme.TitleFontSize - 14, UITheme.TextDark, UITheme.ContentWidth);

            RectTransform chips = UIFactory.CreateRow(content, 14f, 70f);
            UIKit.Chip(chips, Strings.Level + " " + player.Level, UITheme.Card, 260f);
            UIKit.StarChip(chips, player.Stars.ToString(), 260f);
            UIKit.CoinChip(chips, player.Coins.ToString(), 260f);

            UIFactory.CreateLabel(content, Strings.MyCode, UITheme.SmallFontSize + 4, UITheme.Neutral, UITheme.ContentWidth);
            UIKit.Chip(content, player.FriendCode, new Color32(255, 243, 200, 255), 560f, UITheme.BodyFontSize);

            UIFactory.CreateLabel(content, "Açılan bölüm: " + CountDone(levels) + " / " + levels.Catalog.Count, UITheme.BodyFontSize - 6, UITheme.TextDark, UITheme.ContentWidth);

            int owned = 0;
            foreach (RewardDef def in RewardCatalog.All)
            {
                if (rewards.Owns(def.Id)) owned++;
            }
            UIFactory.CreateLabel(content, "Koleksiyon: " + owned + " / " + RewardCatalog.All.Count, UITheme.BodyFontSize - 6, UITheme.TextDark, UITheme.ContentWidth);

            RectTransform stickers = UIFactory.CreateRow(content, 14f, 120f);
            foreach (RewardDef def in RewardCatalog.All)
            {
                if (def.Type != RewardType.Sticker || !rewards.Owns(def.Id)) continue;
                UIKit.StarChip(stickers, def.DisplayName.Replace(" Sticker", string.Empty), 280f, UITheme.SmallFontSize);
            }
        }

        private static int CountDone(LevelManager levels)
        {
            int n = 0;
            for (int i = 1; i <= levels.Catalog.Count; i++)
            {
                if (levels.Stars(i) > 0) n++;
            }
            return n;
        }
    }
}
