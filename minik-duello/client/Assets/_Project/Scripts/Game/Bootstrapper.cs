using MinikDuello.Core;
using UnityEngine;

namespace MinikDuello.Game
{
    /// <summary>
    /// Boot sahnesinin tek giriş noktası: config → log → servis kaydı.
    /// Sonraki fazlarda manager'lar burada kaydedilir.
    /// </summary>
    public sealed class Bootstrapper : MonoBehaviour
    {
        [SerializeField] private GameConfig config;

        private const int TargetFrameRate = 60;

        private void Awake()
        {
            if (config == null)
            {
                Log.Error("Boot", "GameConfig atanmamış.");
                enabled = false;
                return;
            }

            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = TargetFrameRate;
            Log.Configure(config.LogLevel);
            ServiceLocator.Register(config);
            Log.Info("Boot", "Başlatıldı. Ortam: " + config.environment);
        }
    }
}
