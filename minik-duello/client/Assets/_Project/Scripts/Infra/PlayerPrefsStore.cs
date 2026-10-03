using MinikDuello.Services;
using UnityEngine;

namespace MinikDuello.Infra
{
    /// <summary>
    /// Anahtar-değer deposu (PlayerPrefs). UYARI: PlayerPrefs şifreli değildir; saklanan token'lar kısa ömürlü (access) ve
    /// iptal edilebilir (refresh) olsa da yayın öncesi Android Keystore / iOS Keychain ile değiştirilmesi önerilir.
    /// </summary>
    public sealed class PlayerPrefsStore : IKeyValueStore
    {
        public string Get(string key) => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;

        public void Set(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
        }

        public void Clear()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }

        public void Remove(string key)
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }
}
