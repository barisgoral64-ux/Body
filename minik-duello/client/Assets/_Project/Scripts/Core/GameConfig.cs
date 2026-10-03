using UnityEngine;

namespace MinikDuello.Core
{
    /// <summary>
    /// Tüm sayısal ayarlar burada. Kodda magic number kullanılmaz.
    /// Değerler server/src/config/constants.ts ile tutarlı olmalıdır.
    /// </summary>
    [CreateAssetMenu(menuName = "MinikDuello/GameConfig", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Ortam")]
        public AppEnvironment environment = AppEnvironment.Development;
        public string developmentApiUrl = "http://localhost:3000";
        public string developmentWsUrl = "ws://localhost:3000/ws";
        public string productionApiUrl = "https://api.example.invalid";
        public string productionWsUrl = "wss://api.example.invalid/ws";
        public int protocolVersion = 1;

        [Header("Bölümler")]
        public int totalLevels = 100;
        public int levelsPerWorld = 10;
        [Range(0f, 1f)] public float maxDifficultyStep = 0.05f;

        [Header("Ağ")]
        public float httpTimeoutSeconds = 10f;
        public int httpMaxRetries = 2;
        public float reconnectGraceSeconds = 15f;
        public float reconnectBackoffBaseSeconds = 1f;
        public float reconnectBackoffMaxSeconds = 8f;
        public float heartbeatIntervalSeconds = 15f;

        [Header("UI (min dokunma hedefi, dp)")]
        public float minTouchTargetPhone = 96f;
        public float minTouchTargetTablet = 120f;

        [Header("Ebeveyn")]
        public int parentPinLength = 4;

        public bool IsProduction => environment == AppEnvironment.Production;
        public string ApiUrl => IsProduction ? productionApiUrl : developmentApiUrl;
        public string WsUrl => IsProduction ? productionWsUrl : developmentWsUrl;
        public LogLevel LogLevel => IsProduction ? LogLevel.Info : LogLevel.Debug;
    }
}
