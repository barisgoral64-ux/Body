using MinikDuello.Core;
using MinikDuello.Domain.Levels;
using MinikDuello.Services.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace MinikDuello.UI
{
    public sealed class LevelSelectScreen : ScreenBase
    {
        private const int Columns = 4;
        private const float Cell = 218f;

        private Text title;
        private RectTransform content;

        protected override void Build()
        {
            RectTransform row = UIFactory.CreateRow(transform, UITheme.Spacing, TouchTarget);
            UIFactory.CreateButton(row, Strings.Back, UITheme.Neutral, TouchTarget, true, Ui.Back);
            title = UIFactory.CreateLabel(row, string.Empty, UITheme.ButtonFontSize + 4, UITheme.TextDark, 520f);
            content = UIFactory.CreateScroll(transform);
        }

        protected override void OnShown()
        {
            title.text = WorldInfo.Name(Nav.SelectedWorld);
            UIFactory.ClearChildren(content);
            var levels = ServiceLocator.Get<LevelManager>();

            RectTransform grid = UIFactory.CreateGrid(content, Columns, new Vector2(Cell, Cell + 30f), new Vector2(22f, 26f));
            foreach (LevelConfig level in levels.Catalog.InWorld(Nav.SelectedWorld))
            {
                LevelConfig captured = level;
                bool unlocked = levels.IsUnlocked(level.LevelId);
                int stars = levels.Stars(level.LevelId);
                Color color = !unlocked ? UITheme.Locked : (stars >= 3 ? UITheme.Green : UITheme.Primary);

                var go = new GameObject("Level_" + level.LevelId, typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(grid, false);
                var bg = go.GetComponent<Image>();
                bg.sprite = SpriteFactory.RoundedRect();
                bg.type = Image.Type.Sliced;
                bg.color = color;
                go.GetComponent<Button>().targetGraphic = bg;
                go.GetComponent<Button>().onClick.AddListener(() =>
                {
                    if (!unlocked)
                    {
                        Ui.Toast(Strings.Locked);
                        return;
                    }
                    Nav.SelectedLevel = captured.LevelId;
                    Ui.Show(ScreenId.Game);
                });

                Text number = UIFactory.CreateLabel(go.transform, unlocked ? level.LevelId.ToString() : "?", UITheme.TitleFontSize - 6, UITheme.TextLight, 0f);
                number.rectTransform.anchorMin = new Vector2(0f, 0.38f);
                number.rectTransform.anchorMax = Vector2.one;
                number.rectTransform.offsetMin = Vector2.zero;
                number.rectTransform.offsetMax = Vector2.zero;
                Destroy(number.GetComponent<LayoutElement>());

                if (unlocked)
                {
                    RectTransform starsRow = UIKit.StarsRow(go.transform, stars, 3, 46f);
                    starsRow.anchorMin = new Vector2(0.5f, 0f);
                    starsRow.anchorMax = new Vector2(0.5f, 0f);
                    starsRow.pivot = new Vector2(0.5f, 0f);
                    starsRow.anchoredPosition = new Vector2(0f, 14f);
                    starsRow.sizeDelta = new Vector2(180f, 50f);
                }
            }
        }
    }
}
