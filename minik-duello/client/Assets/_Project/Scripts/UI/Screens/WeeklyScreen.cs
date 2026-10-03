using MinikDuello.Core;
using MinikDuello.Domain.Net;
using MinikDuello.Services.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace MinikDuello.UI
{
    /// <summary>Arkadaşlar arası haftalık yıldızlar. Sıra numarası yok: aşırı rekabeti değil, birlikte eğlenmeyi öne çıkarır.</summary>
    public sealed class WeeklyScreen : ScreenBase
    {
        private RectTransform list;
        private Text info;

        protected override void Build()
        {
            BuildHeader(Strings.ThisWeek);
            info = UIFactory.CreateLabel(transform, string.Empty, UITheme.BodyFontSize - 6, UITheme.Neutral, UITheme.ContentWidth);
            list = UIFactory.CreateScroll(transform);
        }

        protected override void OnShown()
        {
            UIFactory.ClearChildren(list);
            info.text = string.Empty;
            RunAsync(async () =>
            {
                var result = await ServiceLocator.Get<FriendManager>().WeeklyAsync();
                if (!IsAlive) return;
                if (!result.IsOk)
                {
                    info.text = Messages.For(result.Error);
                    return;
                }
                foreach (WeeklyEntryDto entry in result.Value) BuildRow(entry);
            }, true);
        }

        private void BuildRow(WeeklyEntryDto entry)
        {
            Image row = UIFactory.CreatePanel(list, "Entry_" + entry.PlayerId, entry.IsSelf ? new Color32(255, 243, 200, 255) : UITheme.Card);
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = UITheme.ContentWidth - 20f;
            le.preferredHeight = 150f;
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 10, 10);
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            UIKit.Avatar(row.transform, entry.AvatarCharacter ?? "panda", null, 110f);
            UIFactory.CreateLabel(row.transform, entry.Username, UITheme.BodyFontSize - 8, UITheme.TextDark, 420f, TextAnchor.MiddleLeft);
            UIKit.StarChip(row.transform, entry.Stars.ToString(), 200f);
        }
    }
}
