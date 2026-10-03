using System.Collections;
using System.Text;
using System.Threading.Tasks;
using MinikDuello.Services;
using UnityEngine;
using UnityEngine.Networking;

namespace MinikDuello.Infra
{
    /// <summary>UnityWebRequest tabanlı HTTP taşıması. Tüm tamamlamalar ana iş parçacığında olur.</summary>
    public sealed class UnityHttpTransport : MonoBehaviour, IHttpTransport
    {
        private const string JsonType = "application/json";
        private const string EmptyJsonBody = "{}";

        public Task<HttpResponse> SendAsync(HttpRequestSpec spec)
        {
            var tcs = new TaskCompletionSource<HttpResponse>();
            StartCoroutine(Run(spec, tcs));
            return tcs.Task;
        }

        private static IEnumerator Run(HttpRequestSpec spec, TaskCompletionSource<HttpResponse> tcs)
        {
            using (var request = new UnityWebRequest(spec.Url, spec.Method))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                bool hasBody = spec.Method == UnityWebRequest.kHttpVerbPOST || spec.Method == UnityWebRequest.kHttpVerbPUT;
                if (hasBody)
                {
                    string body = string.IsNullOrEmpty(spec.Body) ? EmptyJsonBody : spec.Body;
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                    request.SetRequestHeader("Content-Type", JsonType);
                }
                request.SetRequestHeader("Accept", JsonType);
                if (!string.IsNullOrEmpty(spec.BearerToken)) request.SetRequestHeader("Authorization", "Bearer " + spec.BearerToken);
                request.timeout = Mathf.Max(1, Mathf.CeilToInt(spec.TimeoutSeconds));

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.ConnectionError)
                {
                    tcs.TrySetResult(HttpResponse.Network());
                    yield break;
                }

                // ProtocolError dahil sunucudan yanıt geldi: durum kodu ve gövde iletilir.
                tcs.TrySetResult(new HttpResponse
                {
                    StatusCode = (int)request.responseCode,
                    Body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty
                });
            }
        }
    }
}
