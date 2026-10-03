using System;
using UnityEngine;

namespace MinikDuello.UI
{
    /// <summary>Büyük rakam tuşları (PIN girişi için). Klavye kullanılmaz.</summary>
    public static class NumberPad
    {
        private const float CellWidth = 200f;
        private const float Spacing = 16f;

        public static RectTransform Build(Transform parent, float touchTarget, Action<int> onDigit, Action onErase)
        {
            float cellHeight = Mathf.Max(touchTarget, 110f);
            RectTransform grid = UIFactory.CreateGrid(parent, 3, new Vector2(CellWidth, cellHeight), new Vector2(Spacing, Spacing), 4);
            for (int d = 1; d <= 9; d++)
            {
                int digit = d;
                UIFactory.CreateButton(grid, digit.ToString(), UITheme.Blue, touchTarget, true, () => onDigit(digit));
            }
            UIFactory.CreateButton(grid, string.Empty, new Color(0f, 0f, 0f, 0f), touchTarget, true, null).interactable = false;
            UIFactory.CreateButton(grid, "0", UITheme.Blue, touchTarget, true, () => onDigit(0));
            UIFactory.CreateButton(grid, MinikDuello.Core.Strings.Erase, UITheme.Pink, touchTarget, true, () => onErase());
            return grid;
        }

        /// <summary>"* * _ _" biçiminde maskeli gösterim.</summary>
        public static string Masked(int entered, int total)
        {
            var parts = new string[total];
            for (int i = 0; i < total; i++) parts[i] = i < entered ? "*" : "_";
            return string.Join(" ", parts);
        }
    }
}
