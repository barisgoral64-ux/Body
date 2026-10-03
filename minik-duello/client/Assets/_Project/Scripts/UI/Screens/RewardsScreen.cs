using MinikDuello.Core;
using MinikDuello.Domain.Rewards;
using MinikDuello.Services.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace MinikDuello.UI
{
    /// <summary>Koleksiyon ve mağaza. Mağaza yalnızca OYUNDA kazanılan coin kullanır; gerçek para yoktur.</summary>
    public sealed class RewardsScreen : ScreenBase
    {
        private RectTransform content;

        protected override void Build()
        {
            BuildHeader(Strings.Rewards);
            content = UIFactory.CreateScroll(transform);
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
            var rewards = ServiceLocator.Get<RewardManager>();
            var player = ServiceLocator.Get<PlayerManager>();

            RectTransform chips = UIFactory.CreateRow(content, 14f, 70f);
            UIKit.CoinChip(chips, player.Coins + " " + Strings.Coins, 330f);
            UIKit.StarChip(chips, player.Stars + " " + Strings.Stars, 330f);

            foreach (RewardDef def in RewardCatalog.All)
            {
                if (def.Type == RewardType.Character) continue;
                BuildRow(def, rewards, player);
            }
            UIFactory.CreateSpacer(content, 40f);
        }

        private void BuildRow(RewardDef def, RewardManager rewards, PlayerManager player)
        {
            bool owned = rewards.Owns(def.Id);
            Image row = UIFactory.CreatePanel(content, "Reward_" + def.Id, owned ? UITheme.Card : UITheme.CardDim);
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = UITheme.ContentWidth - 40f;
            le.preferredHeight = 170f;
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 14, 14);
            layout.spacing = 24f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            Image icon = UIFactory.CreatePanel(row.transform, "Icon", owned ? ColorFor(def.Type) : UITheme.Locked);
            var iconLe = icon.gameObject.AddComponent<LayoutElement>();
            iconLe.preferredWidth = 120f;
            iconLe.preferredHeight = 120f;
            Text mark = UIFactory.CreateLabel(icon.transform, owned ? def.DisplayName.Substring(0, 1) : "?", UITheme.TitleFontSize - 16, UITheme.TextLight, 0f);
            UIFactory.Stretch(mark.rectTransform);
            Destroy(mark.GetComponent<LayoutElement>());

            RectTransform texts = UIFactory.CreateColumn(row.transform, 4f, 440f);
            UIFactory.CreateLabel(texts, def.DisplayName, UITheme.BodyFontSize - 6, UITheme.TextDark, 440f, TextAnchor.MiddleLeft);
            string status = owned ? Strings.Owned : (def.ShopPrice.HasValue ? def.ShopPrice.Value + " " + Strings.Coins : "Yıldız toplayarak açılır");
            UIFactory.CreateLabel(texts, status, UITheme.SmallFontSize, UITheme.Neutral, 440f, TextAnchor.MiddleLeft);

            if (owned && def.Type != RewardType.Sticker)
            {
                bool on = rewards.IsEquipped(def.Id);
                UIFactory.CreateButton(row.transform, on ? Strings.Unequip : Strings.Equip, on ? UITheme.Neutral : UITheme.Green, TouchTarget, true,
                    () => Toggle(def.Id, !on, rewards));
            }
            else if (!owned && def.ShopPrice.HasValue)
            {
                bool affordable = player.Coins >= def.ShopPrice.Value;
                Button buy = UIFactory.CreateButton(row.transform, Strings.Buy, affordable ? UITheme.Primary : UITheme.Locked, TouchTarget, true, () => Buy(def, affordable, rewards));
                buy.interactable = true;
            }
        }

        private void Toggle(string id, bool equip, RewardManager rewards)
        {
            RunAsync(async () =>
            {
                var result = await rewards.EquipAsync(id, equip);
                if (!result.IsOk) Ui.Toast(Messages.For(result.Error));
                if (IsAlive) Refresh();
            }, true);
        }

        private void Buy(RewardDef def, bool affordable, RewardManager rewards)
        {
            if (!affordable)
            {
                Ui.Toast("Oyun oynayarak coin toplayabilirsin!");
                return;
            }
            RunAsync(async () =>
            {
                var result = await rewards.BuyAsync(def.Id);
                if (result.IsOk)
                {
                    ServiceLocator.Get<MinikDuello.Infra.AudioManager>().Play(MinikDuello.Infra.Sfx.Win);
                    if (Effects.Instance != null) Effects.Instance.BurstAtCenter(false);
                }
                else
                {
                    Ui.Toast(Messages.For(result.Error));
                }
                if (IsAlive) Refresh();
            }, true);
        }

        private static Color ColorFor(RewardType type)
        {
            switch (type)
            {
                case RewardType.Hat: return UITheme.Pink;
                case RewardType.Glasses: return UITheme.Blue;
                case RewardType.Costume: return UITheme.Purple;
                case RewardType.Shoes: return UITheme.Green;
                case RewardType.Backpack: return UITheme.Primary;
                case RewardType.Frame: return UITheme.Yellow;
                case RewardType.Effect: return new Color32(38, 166, 154, 255);
                default: return UITheme.Neutral;
            }
        }
    }
}
