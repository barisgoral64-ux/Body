namespace MinikDuello.Core
{
    /// <summary>
    /// Çalışma zamanı ayarları (Unity'den bağımsız). Tüm sayılar buradan gelir; kodda magic number kullanılmaz.
    /// Değerler server/src/config/constants.ts ile tutarlı olmalıdır.
    /// </summary>
    public sealed class AppSettings
    {
        public AppEnvironment Environment = AppEnvironment.Development;
        public string ApiUrl = "http://localhost:3000";
        public string WsUrl = "ws://localhost:3000/ws";
        public int ProtocolVersion = 1;

        /// <summary>Uygulama sürümü (Application.version). Her API isteğinde X-App-Version olarak gönderilir.</summary>
        public string AppVersion = "0.0.0";
        /// <summary>Paket adı (Application.identifier); mağaza adresi üretmek için.</summary>
        public string PackageName = string.Empty;

        public int TotalLevels = 100;
        public int LevelsPerWorld = 10;

        public float HttpTimeoutSeconds = 10f;
        public int HttpMaxRetries = 2;
        public float HttpRetryBaseSeconds = 0.5f;
        public float ReconnectGraceSeconds = 15f;
        public float ReconnectBackoffBaseSeconds = 1f;
        public float ReconnectBackoffMaxSeconds = 8f;
        public float HeartbeatIntervalSeconds = 15f;
        public float WsAuthTimeoutSeconds = 5f;

        public float MinTouchTargetPhone = 96f;
        public float MinTouchTargetTablet = 120f;
        public int ParentPinLength = 4;

        /// <summary>Tur başında dokunmalar bu süre kilitlidir (yönerge dinlenir); ayrıca sunucunun insan altı hız kontrolüyle uyumludur.</summary>
        public int RoundIntroLockMs = 1600;

        public bool IsProduction => Environment == AppEnvironment.Production;
        public LogLevel LogLevel => IsProduction ? LogLevel.Info : LogLevel.Debug;
    }
}
