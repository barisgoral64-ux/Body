using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MinikDuello.Core;
using MinikDuello.Services;

namespace MinikDuello.Infra
{
    /// <summary>
    /// System.Net.WebSockets.ClientWebSocket taşıması. Ağ işleri arka planda yürür; olaylar MainThreadDispatcher ile
    /// ana iş parçacığına taşınır. Her bağlantı bir "nesil" numarası taşır: eski bağlantıların geç olayları yok sayılır.
    /// </summary>
    public sealed class ClientWebSocketTransport : IWebSocketTransport
    {
        private const int ReceiveBufferBytes = 4096;
        private const int MaxMessageBytes = 64 * 1024;

        private readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
        private ClientWebSocket socket;
        private CancellationTokenSource cancel;
        private int generation;

        public event Action Opened;
        public event Action<string> MessageReceived;
        public event Action<int> Closed;

        public bool IsOpen { get; private set; }

        public void Connect(string url)
        {
            Teardown();
            int gen = ++generation;
            var ws = new ClientWebSocket();
            var cts = new CancellationTokenSource();
            socket = ws;
            cancel = cts;
            Task.Run(() => RunAsync(ws, cts.Token, url, gen));
        }

        public void Send(string text)
        {
            ClientWebSocket ws = socket;
            if (ws == null || ws.State != WebSocketState.Open) return;
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            Task.Run(async () =>
            {
                await sendLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (ws.State == WebSocketState.Open)
                        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    Log.Warn("WS", "Gönderim başarısız: " + exception.GetType().Name);
                }
                finally
                {
                    sendLock.Release();
                }
            });
        }

        public void Close()
        {
            ClientWebSocket ws = socket;
            Teardown();
            if (ws != null && IsOpen)
            {
                IsOpen = false;
                Closed?.Invoke(1000);
            }
        }

        private void Teardown()
        {
            generation++; // bekleyen eski olayları geçersiz kıl
            try
            {
                cancel?.Cancel();
                socket?.Dispose();
            }
            catch (Exception exception)
            {
                Log.Debug("WS", "Kapatma: " + exception.GetType().Name);
            }
            socket = null;
            cancel = null;
        }

        private async Task RunAsync(ClientWebSocket ws, CancellationToken token, string url, int gen)
        {
            int closeCode = 0;
            try
            {
                await ws.ConnectAsync(new Uri(url), token).ConfigureAwait(false);
                Post(gen, () =>
                {
                    IsOpen = true;
                    Opened?.Invoke();
                });

                var buffer = new byte[ReceiveBufferBytes];
                var message = new StringBuilder();
                while (!token.IsCancellationRequested && ws.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        closeCode = result.CloseStatus.HasValue ? (int)result.CloseStatus.Value : 1005;
                        break;
                    }
                    message.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    if (message.Length > MaxMessageBytes)
                    {
                        closeCode = 1009;
                        break;
                    }
                    if (result.EndOfMessage)
                    {
                        string text = message.ToString();
                        message.Length = 0;
                        Post(gen, () => MessageReceived?.Invoke(text));
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return; // bilinçli kapatma: Closed olayı Close() içinde verildi
            }
            catch (Exception exception)
            {
                Log.Debug("WS", "Bağlantı hatası: " + exception.GetType().Name);
            }
            finally
            {
                try
                {
                    ws.Dispose();
                }
                catch (Exception)
                {
                    // zaten kapalı
                }
            }

            int code = closeCode;
            Post(gen, () =>
            {
                IsOpen = false;
                Closed?.Invoke(code);
            });
        }

        private void Post(int gen, Action action)
        {
            MainThreadDispatcher.Post(() =>
            {
                if (gen == generation) action();
            });
        }
    }
}
