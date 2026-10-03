using MinikDuello.Core;
using MinikDuello.Domain.Levels;
using MinikDuello.Services.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace MinikDuello.UI
{
    public sealed class WorldSelectScreen : ScreenBase
    {
        private static readonly Color[] WorldColors =
        {
            new Color32(239, 83, 80, 255), new Color32(255, 167, 38, 255), new Color32(66, 165, 245, 255),
            new Color32(102, 187, 106, 255), new Color32(171, 105, 230, 255), new Color32(38, 166, 154, 255),
            new Color32(255, 112, 67, 255), new Color32(236, 64, 122, 255), new Color32(92, 107, 192, 255),
            new Color32(255, 202, 40, 255)
        };

        private RectTransform content;

        protected override void Build()
        {
            BuildHeader(Strings.Worlds);
            content = UIFactory.CreateScroll(transform);
        }

        protected override void OnShown()
        {
            UIFactory.ClearChildren(content);
            var levels = ServiceLocator.Get<LevelManager>();

            for (int w = 1; w <= WorldInfo.Count; w++)
            {
                int world = w;
                bool unlocked = levels.IsWorldUnlocked(w, Config.LevelsPerWorld);
                Color color = unlocked ? WorldColors[(w - 1) % WorldColors.Length] : UITheme.Locked;
                Button card = UIFactory.CreateButton(content, string.Empty, color, TouchTarget, false, () =>
                {
                    if (!unlocked)
                    {
                        Ui.Toast(Strings.Locked);
                        return;
                    }
                    Nav.SelectedWorld = world;
                    Ui.Show(ScreenId.LevelSelect);
                });
                UIFactory.ClearChildren(card.transform);
                card.GetComponent<LayoutElement>().preferredHeight = 190f;
                card.GetComponent<LayoutElement>().preferredWidth = UITheme.ContentWidth - 60f;

                RectTransform row = UIFactory.CreateRow(card.transform, 24f, 160f, TextAnchor.MiddleLeft);
                UIFactory.Stretch(row);
                row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(30, 30, 10, 10);
                UIFactory.CreateLabel(row, w.ToString(), UITheme.TitleFontSize, UITheme.TextLight, 120f);
                RectTransform texts = UIFactory.CreateColumn(row, 4f, 560f);
                UIFactory.CreateLabel(texts, WorldInfo.Name(w), UITheme.ButtonFontSize - 4, UITheme.TextLight, 560f, TextAnchor.MiddleLeft);
                int stars = levels.WorldStars(w);
                UIFactory.CreateLabel(texts, unlocked ? stars + " / " + Config.LevelsPerWorld * 3 + " " + Strings.Stars : Strings.Locked, UITheme.SmallFontSize + 2, UITheme.TextLight, 560f, TextAnchor.MiddleLeft);
            }
        }
    }
}
