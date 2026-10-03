using System.Collections.Generic;
using MinikDuello.Core;
using MinikDuello.Domain.Rewards;
using MinikDuello.Services.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace MinikDuello.UI
{
    /// <summary>Karakter seçimi ve kozmetik takma (şapka, gözlük, kıyafet, ayakkabı, çanta, efekt).</summary>
    public sealed class CharacterScreen : ScreenBase
    {
        private static readonly string[] SlotNames = { "Şapka", "Gözlük", "Kıyafet", "Ayakkabı", "Sırt Çantası", "Efekt" };

        private RectTransform content;

        protected override void Build()
        {
            BuildHeader(Strings.MyCharacter);
            content = UIFactory.CreateScroll(transform);
        }

        protected override void OnShown()
        {
            Refresh();
            RunAsync(async () =>
            {
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
            Dictionary<string, string> equipped = characters.EquippedBySlot();

            RectTransform preview = UIFactory.CreateRow(content, 0f, 280f);
            UIKit.Avatar(preview, characters.CurrentCharacter, equipped, 260f);
            UIFactory.CreateLabel(content, RewardCatalog.CharacterName(characters.CurrentCharacter), UITheme.BodyFontSize, UITheme.TextDark);

            UIFactory.CreateLabel(content, "Karakter seç", UITheme.BodyFontSize, UITheme.Neutral);
            RectTransform grid = UIFactory.CreateGrid(content, 4, new Vector2(220f, 240f), new Vector2(20f, 20f));
            foreach (string id in RewardCatalog.CharacterIds)
            {
                string characterId = id;
                bool owned = characters.IsCharacterOwned(id);
                bool current = characters.CurrentCharacter == id;
                var go = new GameObject("Char_" + id, typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(grid, false);
                var bg = go.GetComponent<Image>();
                bg.sprite = SpriteFactory.RoundedRect();
                bg.type = Image.Type.Sliced;
                bg.color = current ? new Color32(200, 235, 200, 255) : (owned ? UITheme.Card : UITheme.CardDim);
                go.GetComponent<Button>().targetGraphic = bg;
                go.GetComponent<Button>().onClick.AddListener(() => SelectCharacter(characterId, owned));

                RectTransform avatar = UIKit.Avatar(go.transform, id, null, 140f);
                avatar.anchorMin = avatar.anchorMax = new Vector2(0.5f, 0.62f);
                avatar.anchoredPosition = Vector2.zero;
                Text name = UIFactory.CreateLabel(go.transform, owned ? RewardCatalog.CharacterName(id) : Strings.Locked, UITheme.SmallFontSize, UITheme.TextDark, 0f);
                name.rectTransform.anchorMin = new Vector2(0f, 0f);
                name.rectTransform.anchorMax = new Vector2(1f, 0.22f);
                name.rectTransform.offsetMin = Vector2.zero;
                name.rectTransform.offsetMax = Vector2.zero;
                Destroy(name.GetComponent<LayoutElement>());
            }

            UIFactory.CreateLabel(content, "Giysiler", UITheme.BodyFontSize, UITheme.Neutral);
            for (int i = 0; i < RewardCatalog.CosmeticSlots.Length; i++)
            {
                string slot = RewardCatalog.CosmeticSlots[i];
                string slotName = SlotNames[i];
                equipped.TryGetValue(slot, out string currentId);
                string label = slotName + ": " + (currentId != null ? RewardCatalog.Find(currentId).DisplayName : "Yok");
                UIFactory.CreateButton(content, label, UITheme.Blue, TouchTarget, true, () => CycleSlot(slot, rewards));
            }
            UIFactory.CreateSpacer(content, 40f);
        }

        private void SelectCharacter(string characterId, bool owned)
        {
            if (!owned)
            {
                Ui.Toast("Yıldız toplayarak yeni karakterler açılır!");
                return;
            }
            RunAsync(async () =>
            {
                var result = await ServiceLocator.Get<RewardManager>().EquipAsync(RewardCatalog.CharacterPrefix + characterId, true);
                if (!result.IsOk) Ui.Toast(Messages.For(result.Error));
                if (IsAlive) Refresh();
            }, true);
        }

        private void CycleSlot(string slot, RewardManager rewards)
        {
            var options = new List<string> { null };
            foreach (RewardDef def in RewardCatalog.All)
            {
                if (def.Slot == slot && rewards.Owns(def.Id)) options.Add(def.Id);
            }
            if (options.Count == 1)
            {
                Ui.Toast("Henüz bir şeyin yok. Ödüller bölümüne bak!");
                return;
            }

            string current = null;
            foreach (string id in options)
            {
                if (id != null && rewards.IsEquipped(id)) current = id;
            }
            int next = (options.IndexOf(current) + 1) % options.Count;
            string target = options[next];

            RunAsync(async () =>
            {
                var result = target != null
                    ? await rewards.EquipAsync(target, true)
                    : await rewards.EquipAsync(current, false);
                if (!result.IsOk) Ui.Toast(Messages.For(result.Error));
                if (IsAlive) Refresh();
            }, true);
        }
    }
}
