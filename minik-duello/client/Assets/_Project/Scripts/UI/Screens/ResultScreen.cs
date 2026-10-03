using System.Collections;
using MinikDuello.Core;
using MinikDuello.Domain.Levels;
using MinikDuello.Domain.Rewards;
using MinikDuello.Infra;
using MinikDuello.Services;
using MinikDuello.Services.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace MinikDuello.UI
{
    /// <summary>Bölüm sonucu: yıldızlar, ödüller ve devam düğmeleri. Olumsuz mesaj yoktur.</summary>
    public sealed class ResultScreen : ScreenBase
    {
        private const float StarRevealSeconds = 0.4f;

        private RectTransform content;
        private Coroutine reveal;

        protected override void Build()
        {
            content = UIFactory.CreateColumn(transform, UITheme.Spacing);
            UIFactory.CreateSpacer(transform, 10f);
        }

        protected override void OnShown()
        {
            EventBus.Subscribe<LevelServerResult>(OnServerResult);
            Rebuild();
        }

        protected override void OnHidden()
        {
            EventBus.Unsubscribe<LevelServerResult>(OnServerResult);
            if (reveal != null) StopCoroutine(reveal);
            reveal = null;
        }

        private void OnServerResult(LevelServerResult result)
        {
            if (result.Completion == Nav.LastCompletion && IsAlive) Rebuild(false);
        }

        private void Rebuild(bool animate = true)
        {
            UIFactory.ClearChildren(content);
            LevelCompletion c = Nav.LastCompletion;
            if (c == null)
            {
                Ui.GoHome();
                return;
            }

            UIFactory.CreateLabel(content, TitleFor(c.Stars), UITheme.TitleFontSize, UITheme.TextDark, UITheme.ContentWidth);
            RectTransform stars = UIKit.StarsRow(content, 0, 3, 200f);
            UIFactory.CreateLabel(content, Nav.LastScore + " puan", UITheme.BodyFontSize, UITheme.TextDark);

            if (animate)
            {
                if (reveal != null) StopCoroutine(reveal);
                reveal = StartCoroutine(RevealStars(stars, c.Stars));
            }
            else
            {
                Fill(stars, c.Stars);
            }

            BuildRewards(c);
            BuildButtons(c);
        }

        private void BuildRewards(LevelCompletion c)
        {
            if (c.Server != null)
            {
                if (c.Server.CoinsGained > 0) UIKit.CoinChip(content, "+" + c.Server.CoinsGained + " " + Strings.Coins, 520f, UITheme.BodyFontSize - 6);
                foreach (string id in c.Server.NewRewards)
                {
                    RewardDef def = RewardCatalog.Find(id);
                    UIKit.StarChip(content, def != null ? def.DisplayName : id, 640f, UITheme.BodyFontSize - 6);
                }
            }
            else if (!ServiceLocator.Get<ConnectivityState>().IsOnline)
            {
                UIFactory.CreateLabel(content, "İlerlemen kaydedildi. İnternet gelince ödüllerin eklenecek!", UITheme.SmallFontSize + 2, UITheme.Neutral, UITheme.ContentWidth);
            }
        }

        private void BuildButtons(LevelCompletion c)
        {
            if (c.NextLevelId.HasValue)
            {
                UIFactory.CreateButton(content, Strings.Next, UITheme.Green, TouchTarget, false, () =>
                {
                    Nav.SelectedLevel = c.NextLevelId.Value;
                    Nav.SelectedWorld = ServiceLocator.Get<LevelManager>().Catalog.Get(c.NextLevelId.Value).WorldId;
                    Ui.Replace(ScreenId.Game);
                });
            }
            UIFactory.CreateButton(content, Strings.PlayAgain, UITheme.Primary, TouchTarget, false, () => Ui.Replace(ScreenId.Game));
            UIFactory.CreateButton(content, Strings.Menu, UITheme.Neutral, TouchTarget, true, Ui.GoHome);
        }

        private static string TitleFor(int stars)
        {
            if (stars >= 3) return "Harika!";
            if (stars == 2) return "Çok güzel!";
            return "Güzel gidiyorsun!";
        }

        private IEnumerator RevealStars(RectTransform row, int filled)
        {
            var audio = ServiceLocator.Get<AudioManager>();
            for (int i = 0; i < filled; i++)
            {
                yield return new WaitForSecondsRealtime(StarRevealSeconds);
                if (row == null) yield break;
                Image star = row.GetChild(i).GetComponent<Image>();
                star.color = UITheme.Yellow;
                audio.Play(Sfx.Star);
                if (Effects.Instance != null) Effects.Instance.Pop(star.rectTransform);
            }
            if (filled >= 3 && Effects.Instance != null)
            {
                audio.Play(Sfx.Win);
                Effects.Instance.BurstAtCenter(false);
            }
        }

        private static void Fill(RectTransform row, int filled)
        {
            for (int i = 0; i < row.childCount; i++) row.GetChild(i).GetComponent<Image>().color = i < filled ? UITheme.Yellow : new Color32(210, 204, 190, 255);
        }
    }
}
