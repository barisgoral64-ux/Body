using MinikDuello.Core;
using MinikDuello.UI;
using UnityEngine;

namespace MinikDuello.Game
{
    /// <summary>
    /// Uygulamanın tek giriş noktası. Hangi sahne açık olursa olsun otomatik başlar:
    /// config → log → servis kaydı → UI. Elle sahne kurulumu gerekmez.
    /// </summary>
    public sealed class Bootstrapper : MonoBehaviour
    {
        private const string ConfigResourceName = "GameConfig";
        private const int TargetFrameRate = 60;

        private static bool launched;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (launched) return;
            launched = true;
            new GameObject("Bootstrapper", typeof(Bootstrapper));
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = TargetFrameRate;

            GameConfig config = LoadConfig();
            Log.Configure(config.LogLevel);
            ServiceLocator.Register(config);

            var ui = new GameObject("UIManager").AddComponent<UIManager>();
            ui.transform.SetParent(transform, false);
            ui.Initialize(config);
            ServiceLocator.Register(ui);

            Log.Info("Boot", "Başlatıldı. Ortam: " + config.environment);
        }

        private void OnDestroy() => launched = false;

        private static GameConfig LoadConfig()
        {
            var config = Resources.Load<GameConfig>(ConfigResourceName);
            if (config != null) return config;

            // Varlık yoksa güvenli varsayılanlarla (Development) devam edilir.
            Debug.LogWarning("Resources/GameConfig bulunamadı; varsayılan ayarlar kullanılıyor.");
            return ScriptableObject.CreateInstance<GameConfig>();
        }
    }
}
