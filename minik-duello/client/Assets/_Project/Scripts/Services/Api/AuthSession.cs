using System;
using System.Threading.Tasks;
using MinikDuello.Core;
using MinikDuello.Domain.Net;

namespace MinikDuello.Services.Api
{
    /// <summary>
    /// Anonim oturum: cihaz kimliği ile kayıt, token saklama ve yenileme. E-posta/telefon/isim toplanmaz.
    /// Refresh başarısız olursa aynı cihaz kimliğiyle yeniden kayıt aynı hesabı döndürür.
    /// </summary>
    public sealed class AuthSession
    {
        private const string KeyAccess = "auth.access";
        private const string KeyRefresh = "auth.refresh";
        private const string KeyDevice = "auth.device";
        private const string KeyPlayer = "auth.player";
        private const string KeyCode = "auth.code";
        private const string KeyName = "auth.name";

        private readonly AppSettings settings;
        private readonly IHttpTransport http;
        private readonly IKeyValueStore store;
        private Task<bool> refreshing;

        public AuthSession(AppSettings settings, IHttpTransport http, IKeyValueStore store)
        {
            this.settings = settings;
            this.http = http;
            this.store = store;
        }

        public bool HasSession => !string.IsNullOrEmpty(store.Get(KeyAccess));
        public string PlayerId => store.Get(KeyPlayer);
        public string FriendCode => store.Get(KeyCode);
        public string Username => store.Get(KeyName);
        public string AccessToken => store.Get(KeyAccess);

        public event Action SessionLost;

        public string DeviceId
        {
            get
            {
                string id = store.Get(KeyDevice);
                if (string.IsNullOrEmpty(id))
                {
                    id = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N").Substring(0, 8);
                    store.Set(KeyDevice, id);
                }
                return id;
            }
        }

        /// <summary>İlk açılışta (veya oturum yoksa) hesap açar. Çevrimdışıysa başarısız döner, oyun yine de çevrimdışı oynanır.</summary>
        public async Task<Result<bool>> EnsureRegisteredAsync()
        {
            if (HasSession) return Result<bool>.Ok(true);
            return await RegisterAsync();
        }

        public async Task<string> GetAccessTokenAsync()
        {
            if (!HasSession) await RegisterAsync();
            return store.Get(KeyAccess);
        }

        /// <summary>Tek uçuşlu yenileme: eşzamanlı 401'ler tek istek paylaşır.</summary>
        public Task<bool> RefreshAsync()
        {
            if (refreshing == null || refreshing.IsCompleted) refreshing = RefreshCoreAsync();
            return refreshing;
        }

        public void Clear()
        {
            store.Remove(KeyAccess);
            store.Remove(KeyRefresh);
        }

        private async Task<bool> RefreshCoreAsync()
        {
            string refresh = store.Get(KeyRefresh);
            if (!string.IsNullOrEmpty(refresh))
            {
                HttpResponse response = await http.SendAsync(Request("/v1/auth/refresh", Json.Serialize(new { refreshToken = refresh })));
                if (response.IsSuccess)
                {
                    TokensDto tokens = Json.Deserialize<TokensDto>(response.Body);
                    if (tokens != null && !string.IsNullOrEmpty(tokens.AccessToken))
                    {
                        Save(tokens);
                        return true;
                    }
                }
                if (response.NetworkError) return false;
            }

            // Refresh token geçersiz/süresi dolmuş: aynı cihaz kimliğiyle yeniden kayıt aynı hesabı döndürür.
            Result<bool> registered = await RegisterAsync();
            if (!registered.IsOk) SessionLost?.Invoke();
            return registered.IsOk;
        }

        private async Task<Result<bool>> RegisterAsync()
        {
            HttpResponse response = await http.SendAsync(Request("/v1/auth/anonymous", Json.Serialize(new { deviceId = DeviceId })));
            if (response.NetworkError) return Result<bool>.Fail(ErrorCode.Network, "Bağlantı yok");
            if (!response.IsSuccess)
            {
                ErrorDto error = TryParse<ErrorDto>(response.Body);
                return Result<bool>.Fail(ErrorCodes.FromServer(error?.Code, response.StatusCode), "Kayıt başarısız");
            }

            RegisterResponseDto data = Json.Deserialize<RegisterResponseDto>(response.Body);
            if (data?.Tokens == null || data.Player == null) return Result<bool>.Fail(ErrorCode.Internal, "Geçersiz yanıt");
            Save(data.Tokens);
            store.Set(KeyPlayer, data.Player.PlayerId);
            store.Set(KeyCode, data.Player.FriendCode);
            store.Set(KeyName, data.Player.Username);
            Log.Info("Auth", data.IsNew ? "Yeni hesap oluşturuldu." : "Mevcut hesaba dönüldü.");
            return Result<bool>.Ok(true);
        }

        private void Save(TokensDto tokens)
        {
            store.Set(KeyAccess, tokens.AccessToken);
            if (!string.IsNullOrEmpty(tokens.RefreshToken)) store.Set(KeyRefresh, tokens.RefreshToken);
        }

        private HttpRequestSpec Request(string path, string body) => new HttpRequestSpec
        {
            Method = "POST",
            Url = settings.ApiUrl + path,
            Body = body,
            TimeoutSeconds = settings.HttpTimeoutSeconds
        };

        private static T TryParse<T>(string json) where T : class
        {
            try
            {
                return string.IsNullOrEmpty(json) ? null : Json.Deserialize<T>(json);
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return null;
            }
        }
    }
}
