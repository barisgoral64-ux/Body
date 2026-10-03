using MinikDuello.Core;
using MinikDuello.Domain.Net;
using MinikDuello.Domain.Rewards;
using MinikDuello.Infra;
using MinikDuello.Services.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace MinikDuello.UI
{
    /// <summary>Günlük ödül takvimi. Baskı yok: kaçırılan günler seriyi bozmaz, takvim kaldığı yerden devam eder.</summary>
    public sealed class DailyRewardScreen : ScreenBase
    {
        private RectTransform content;
        private int nextDay;
        private bool canClaim;

        protected override void Build()
        {
            BuildHeader(Strings.Daily);
            content = UIFactory.CreateColumn(transform, UITheme.Spacing);
        }

        protected override void OnShown()
        {
            Refresh(0, false);
            RunAsync(async () =>
            {
                var status = await ServiceLocator.Get<RewardManager>().DailyStatusAsync();
                if (!IsAlive) return;
                if (status.IsOk) Refresh(status.Value.NextDayIndex, status.Value.CanClaim);
                else Ui.Toast(Messages.For(status.Error));
            });
        }

        private void Refresh(int day, bool claimable)
        {
            nextDay = day;
            canClaim = claimable;
            UIFactory.ClearChildren(content);

            RectTransform grid = UIFactory.CreateGrid(content, 4, new Vector2(220f, 200f), new Vector2(22f, 22f));
            for (int i = 0; i < DailyCycle.Days.Length; i++)
            {
                DailyEntry entry = DailyCycle.Days[i];
                bool isNext = i == nextDay;
                var go = new GameObject("Day_" + i, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(grid, false);
                var bg = go.GetComponent<Image>();
                bg.sprite = SpriteFactory.RoundedRect();
                bg.type = Image.Type.Sliced;
                bg.color = isNext ? new Color32(255, 236, 170, 255) : (i < nextDay ? new Color32(200, 235, 200, 255) : UITheme.Card);

                Text dayLabel = UIFactory.CreateLabel(go.transform, (i + 1) + ". gün", UITheme.SmallFontSize, UITheme.Neutral, 0f);
                dayLabel.rectTransform.anchorMin = new Vector2(0f, 0.7f);
                dayLabel.rectTransform.anchorMax = Vector2.one;
                dayLabel.rectTransform.offsetMin = Vector2.zero;
                dayLabel.rectTransform.offsetMax = Vector2.zero;
                Destroy(dayLabel.GetComponent<LayoutElement>());

                string text = entry.Coins > 0 ? entry.Coins + " " + Strings.Coins : RewardCatalog.Find(entry.RewardId).DisplayName.Replace(" Sticker", string.Empty);
                Text value = UIFactory.CreateLabel(go.transform, text, UITheme.SmallFontSize + 2, UITheme.TextDark, 0f);
                value.rectTransform.anchorMin = Vector2.zero;
                value.rectTransform.anchorMax = new Vector2(1f, 0.7f);
                value.rectTransform.offsetMin = Vector2.zero;
                value.rectTransform.offsetMax = Vector2.zero;
                Destroy(value.GetComponent<LayoutElement>());
            }

            if (canClaim) UIFactory.CreateButton(content, Strings.Claim, UITheme.Primary, TouchTarget, false, Claim);
            else UIFactory.CreateLabel(content, "Bugünün ödülünü aldın. Yarın tekrar gel!", UITheme.BodyFontSize - 6, UITheme.Neutral, UITheme.ContentWidth);
        }

        private void Claim()
        {
            RunAsync(async () =>
            {
                var result = await ServiceLocator.Get<RewardManager>().ClaimDailyAsync();
                if (!IsAlive) return;
                if (result.IsOk)
                {
                    ServiceLocator.Get<AudioManager>().Play(Sfx.Win);
                    if (Effects.Instance != null) Effects.Instance.BurstAtCenter(false);
                    Refresh((nextDay + 1) % DailyCycle.Days.Length, false);
                }
                else
                {
                    Ui.Toast(Messages.For(result.Error));
                }
            }, true);
        }
    }
}
