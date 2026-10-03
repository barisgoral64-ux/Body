using System;
using System.Threading.Tasks;

namespace MinikDuello.Services
{
    public sealed class HttpRequestSpec
    {
        public string Method;
        public string Url;
        public string Body;
        public string BearerToken;
        public float TimeoutSeconds;
        public string AppVersion;
    }

    public sealed class HttpResponse
    {
        public int StatusCode;
        public string Body;
        /// <summary>Sunucuya hiç ulaşılamadı (bağlantı yok / zaman aşımı).</summary>
        public bool NetworkError;

        public static HttpResponse Network() => new HttpResponse { NetworkError = true };
        public bool IsSuccess => !NetworkError && StatusCode >= 200 && StatusCode < 300;
    }

    public interface IHttpTransport
    {
        Task<HttpResponse> SendAsync(HttpRequestSpec spec);
    }

    /// <summary>
    /// WebSocket taşıma soyutlaması. Olaylar MUTLAKA ana iş parçacığında tetiklenir (Unity uygulaması dağıtıcı kullanır).
    /// </summary>
    public interface IWebSocketTransport
    {
        event Action Opened;
        event Action<string> MessageReceived;
        /// <summary>Kapanış kodu; 0 = bağlantı kurulamadı/koptu.</summary>
        event Action<int> Closed;
        bool IsOpen { get; }
        void Connect(string url);
        void Send(string text);
        void Close();
    }

    public interface IKeyValueStore
    {
        string Get(string key);
        void Set(string key, string value);
        void Remove(string key);
        /// <summary>Tüm yerel veriyi siler (hesap silme).</summary>
        void Clear();
    }

    public interface IScheduler
    {
        double NowSeconds { get; }
        Task DelayAsync(double seconds);
        IDisposable After(double seconds, Action action);
        IDisposable Every(double seconds, Action action);
    }

    public interface IClock
    {
        DateTime UtcNow { get; }
        DateTime LocalNow { get; }
    }

    /// <summary>Sesli yönerge. Gerçek kayıtlar eklenene kadar NullVoice kullanılır.</summary>
    public interface IVoiceService
    {
        void Speak(string clipKey);
    }

    public sealed class NullVoice : IVoiceService
    {
        public void Speak(string clipKey)
        {
        }
    }
}
