using MinikDuello.Core;
using MinikDuello.Domain.Net;
using MinikDuello.Services.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace MinikDuello.UI
{
    /// <summary>
    /// Arkadaş kodu ile istek: çocuk klavye kullanmaz; hayvanı seçer, rakamlara dokunur. Kişisel bilgi girilmez.
    /// </summary>
    public sealed class AddFriendScreen : ScreenBase
    {
        private Text myCode;
        private Text preview;
        private string animal;
        private string digits = string.Empty;
        private Button sendButton;
        private RectTransform animalGrid;

        protected override void Build()
        {
            BuildHeader(Strings.AddFriend);
            UIFactory.CreateLabel(transform, Strings.MyCode, UITheme.SmallFontSize + 4, UITheme.Neutral, UITheme.ContentWidth);
            myCode = UIKit.Chip(transform, string.Empty, new Color32(255, 243, 200, 255), 620f, UITheme.BodyFontSize);

            UIFactory.CreateLabel(transform, "Arkadaşının kodunu seç:", UITheme.BodyFontSize - 6, UITheme.TextDark, UITheme.ContentWidth);
            preview = UIFactory.CreateLabel(transform, string.Empty, UITheme.TitleFontSize - 10, UITheme.Primary, UITheme.ContentWidth);

            animalGrid = UIFactory.CreateGrid(transform, 4, new Vector2(220f, 110f), new Vector2(14f, 14f), 2);
            for (int i = 0; i < FriendCodes.AnimalKeys.Length; i++)
            {
                string key = FriendCodes.AnimalKeys[i];
                UIFactory.CreateButton(animalGrid, FriendCodes.AnimalNames[i], UITheme.Blue, 100f, true, () =>
                {
                    animal = key;
                    Refresh();
                });
            }

            RectTransform pad = UIFactory.CreateGrid(transform, 5, new Vector2(170f, 110f), new Vector2(12f, 12f), 2);
            foreach (char d in FriendCodes.AllowedDigits)
            {
                char digit = d;
                UIFactory.CreateButton(pad, digit.ToString(), UITheme.Purple, 100f, true, () => AddDigit(digit));
            }
            UIFactory.CreateButton(pad, Strings.Erase, UITheme.Pink, 100f, true, Erase);
            UIFactory.CreateButton(pad, "TEMİZLE", UITheme.Neutral, 100f, true, Clear);

            sendButton = UIFactory.CreateButton(transform, "İSTEK GÖNDER", UITheme.Green, TouchTarget, false, Send);
        }

        protected override void OnShown()
        {
            myCode.text = ServiceLocator.Get<PlayerManager>().FriendCode;
            Clear();
        }

        private void AddDigit(char d)
        {
            if (digits.Length >= FriendCodes.DigitCount) return;
            digits += d;
            Refresh();
        }

        private void Erase()
        {
            if (digits.Length > 0) digits = digits.Substring(0, digits.Length - 1);
            else animal = null;
            Refresh();
        }

        private void Clear()
        {
            animal = null;
            digits = string.Empty;
            Refresh();
        }

        private void Refresh()
        {
            preview.text = FriendCodes.Preview(animal, digits);
            bool complete = animal != null && digits.Length == FriendCodes.DigitCount;
            sendButton.interactable = complete;
            sendButton.GetComponent<Image>().color = complete ? UITheme.Green : UITheme.Locked;
        }

        private void Send()
        {
            string code = FriendCodes.Format(animal, digits);
            if (!FriendCodes.IsValid(code)) return;
            RunAsync(async () =>
            {
                var result = await ServiceLocator.Get<FriendManager>().SendRequestAsync(code);
                if (!IsAlive) return;
                if (result.IsOk)
                {
                    // Sunucu bilerek nötr yanıt verir: kod bulunsa da bulunmasa da aynı mesaj.
                    Ui.Toast("İstek gönderildi! Arkadaşın kabul edince eklenir.");
                    Clear();
                }
                else
                {
                    Ui.Toast(Messages.For(result.Error));
                }
            }, true);
        }
    }
}
