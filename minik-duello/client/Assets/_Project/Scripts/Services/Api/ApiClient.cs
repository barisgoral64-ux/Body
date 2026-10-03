using System;
using System.Threading.Tasks;
using MinikDuello.Core;
using MinikDuello.Domain.Net;
using Newtonsoft.Json;

namespace MinikDuello.Services.Api
{
    /// <summary>Boş yanıt gövdesi için işaret türü.</summary>
    public sealed class Unit
    {
        public static readonly Unit Value = new Unit();
    }

    /// <summary>
    /// Tipli REST istemcisi. Ağ kopmasında üssel geri çekilmeyle yeniden dener; 401'de token yeniler ve bir kez tekrar dener.
    /// Hata ayrıntısı kullanıcıya gösterilmez: yalnızca ErrorCode döner, UI ikon/ses seçer.
    /// </summary>
    public sealed class ApiClient
    {
        private readonly AppSettings settings;
        private readonly IHttpTransport http;
        private readonly AuthSession auth;
        private readonly IScheduler scheduler;
        private readonly ConnectivityState connectivity;

        public ApiClient(AppSettings settings, IHttpTransport http, AuthSession auth, IScheduler scheduler, ConnectivityState connectivity = null)
        {
            this.connectivity = connectivity;
            this.settings = settings;
            this.http = http;
            this.auth = auth;
            this.scheduler = scheduler;
        }

        public Task<Result<T>> GetAsync<T>(string path) => SendAsync<T>("GET", path, null);
        public Task<Result<T>> PostAsync<T>(string path, object body) => SendAsync<T>("POST", path, body);
        public Task<Result<T>> PutAsync<T>(string path, object body) => SendAsync<T>("PUT", path, body);

        private async Task<Result<T>> SendAsync<T>(string method, string path, object body)
        {
            string json = body == null ? null : Json.Serialize(body);
            int networkFailures = 0;
            bool refreshed = false;

            while (true)
            {
                string token = await auth.GetAccessTokenAsync();
                HttpResponse response = await http.SendAsync(new HttpRequestSpec
                {
                    Method = method,
                    Url = settings.ApiUrl + path,
                    Body = json,
                    BearerToken = token,
                    TimeoutSeconds = settings.HttpTimeoutSeconds,
                    AppVersion = settings.AppVersion
                });

                connectivity?.Report(!response.NetworkError);
                if (response.NetworkError)
                {
                    if (networkFailures >= settings.HttpMaxRetries) return Result<T>.Fail(ErrorCode.Network, "Bağlantı yok");
                    double wait = settings.HttpRetryBaseSeconds * Math.Pow(2, networkFailures);
                    networkFailures++;
                    await scheduler.DelayAsync(wait);
                    continue;
                }

                // 426: sunucu bu uygulama sürümünü artık desteklemiyor.
                if (response.StatusCode == 426) connectivity?.ReportUpdateRequired();

                if (response.StatusCode == 401 && !refreshed)
                {
                    refreshed = true;
                    if (await auth.RefreshAsync()) continue;
                }

                return Parse<T>(response);
            }
        }

        private static Result<T> Parse<T>(HttpResponse response)
        {
            if (response.IsSuccess)
            {
                if (typeof(T) == typeof(Unit)) return Result<T>.Ok((T)(object)Unit.Value);
                try
                {
                    T value = Json.Deserialize<T>(response.Body);
                    return value != null ? Result<T>.Ok(value) : Result<T>.Fail(ErrorCode.Internal, "Boş yanıt");
                }
                catch (JsonException)
                {
                    return Result<T>.Fail(ErrorCode.Internal, "Bozuk yanıt");
                }
            }

            string code = null;
            try
            {
                code = Json.Deserialize<ErrorDto>(response.Body)?.Code;
            }
            catch (JsonException)
            {
                // Gövde JSON değilse durum koduna göre eşlenir.
            }
            return Result<T>.Fail(ErrorCodes.FromServer(code, response.StatusCode), "Sunucu hatası " + response.StatusCode);
        }
    }
}
