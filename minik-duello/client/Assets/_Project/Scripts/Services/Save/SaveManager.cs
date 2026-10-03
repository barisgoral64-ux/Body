using System;
using MinikDuello.Core;
using MinikDuello.Domain.Net;
using Newtonsoft.Json;

namespace MinikDuello.Services.Save
{
    /// <summary>Yerel kayıt yöneticisi. Bozuk kayıt oyunu çökertmez: yedeklenir ve temiz kayıtla devam edilir.</summary>
    public sealed class SaveManager
    {
        private const string Key = "save.v1";
        private const string BackupKey = "save.v1.corrupt";

        private readonly IKeyValueStore store;

        public SaveData Data { get; private set; }

        public SaveManager(IKeyValueStore store)
        {
            this.store = store;
            Data = Load();
        }

        public void Update(Action<SaveData> mutate)
        {
            mutate(Data);
            Save();
        }

        /// <summary>Tüm yerel kaydı sıfırlar (hesap silme).</summary>
        public void Reset()
        {
            Data = new SaveData();
            Save();
        }

        public void Save() => store.Set(Key, Json.Serialize(Data));

        private SaveData Load()
        {
            string raw = store.Get(Key);
            if (string.IsNullOrEmpty(raw)) return new SaveData();
            try
            {
                SaveData data = Json.Deserialize<SaveData>(raw);
                if (data == null) throw new JsonException("boş kayıt");
                Normalize(data);
                return data;
            }
            catch (JsonException exception)
            {
                Log.Error("Save", "Kayıt okunamadı, yedeklenip sıfırlanıyor: " + exception.Message);
                store.Set(BackupKey, raw);
                return new SaveData();
            }
        }

        private static void Normalize(SaveData data)
        {
            if (data.Levels == null) data.Levels = new System.Collections.Generic.Dictionary<int, LevelRecord>();
            if (data.Pending == null) data.Pending = new System.Collections.Generic.List<PendingResult>();
            if (data.StruggleStreaks == null) data.StruggleStreaks = new System.Collections.Generic.Dictionary<int, int>();
            if (data.PlayMinutes == null) data.PlayMinutes = new System.Collections.Generic.Dictionary<string, double>();
            if (data.OwnedRewards == null) data.OwnedRewards = new System.Collections.Generic.List<string>();
            if (data.EquippedRewards == null) data.EquippedRewards = new System.Collections.Generic.List<string>();
            data.MusicVolume = Math.Max(0, Math.Min(100, data.MusicVolume));
            data.SfxVolume = Math.Max(0, Math.Min(100, data.SfxVolume));
        }
    }
}
