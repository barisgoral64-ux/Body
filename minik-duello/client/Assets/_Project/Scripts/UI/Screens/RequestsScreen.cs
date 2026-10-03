using MinikDuello.Core;
using MinikDuello.Domain.Net;
using MinikDuello.Services.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace MinikDuello.UI
{
    /// <summary>Gelen arkadaş istekleri (yetişkin kapısından sonra): kabul et / reddet.</summary>
    public sealed class RequestsScreen : ScreenBase
    {
        private RectTransform list;
        private Text info;

        protected override void Build()
        {
            BuildHeader(Strings.Requests);
            info = UIFactory.CreateLabel(transform, string.Empty, UITheme.BodyFontSize - 6, UITheme.Neutral, UITheme.ContentWidth);
            list = UIFactory.CreateScroll(transform);
        }

        protected override void OnShown() => Load();

        private void Load()
        {
            UIFactory.ClearChildren(list);
            info.text = string.Empty;
            RunAsync(async () =>
            {
                var result = await ServiceLocator.Get<FriendManager>().IncomingAsync();
                if (!IsAlive) return;
                if (!result.IsOk)
                {
                    info.text = Messages.For(result.Error);
                    return;
                }
                info.text = result.Value.Count == 0 ? "Bekleyen istek yok." : string.Empty;
                foreach (IncomingRequestDto request in result.Value) BuildRow(request);
            }, true);
        }

        private void BuildRow(IncomingRequestDto request)
        {
            Image row = UIFactory.CreatePanel(list, "Request_" + request.RequestId, UITheme.Card);
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = UITheme.ContentWidth - 20f;
            le.preferredHeight = 200f;
            var layout = row.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 12, 12);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            UIFactory.CreateLabel(row.transform, request.From.Username, UITheme.BodyFontSize - 6, UITheme.TextDark, 800f);
            RectTransform buttons = UIFactory.CreateRow(row.transform, 16f, 90f);
            UIFactory.CreateButton(buttons, Strings.Accept, UITheme.Green, 90f, true, () => Respond(request.RequestId, true));
            UIFactory.CreateButton(buttons, Strings.Decline, UITheme.Neutral, 90f, true, () => Respond(request.RequestId, false));
        }

        private void Respond(string requestId, bool accept)
        {
            RunAsync(async () =>
            {
                var result = await ServiceLocator.Get<FriendManager>().RespondAsync(requestId, accept);
                if (!result.IsOk) Ui.Toast(Messages.For(result.Error));
                else if (accept) Ui.Toast("Yeni arkadaş eklendi!");
                if (IsAlive) Load();
            }, true);
        }
    }
}
