using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MinikDuello.Services;

namespace MinikDuello.Tests
{
    public sealed class FakeStore : IKeyValueStore
    {
        public readonly Dictionary<string, string> Data = new Dictionary<string, string>();
        public string Get(string key) => Data.TryGetValue(key, out string v) ? v : null;
        public void Set(string key, string value) => Data[key] = value;
        public void Remove(string key) => Data.Remove(key);
    }

    public sealed class FakeClock : IClock
    {
        public DateTime Now = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Local);
        public DateTime UtcNow => Now.ToUniversalTime();
        public DateTime LocalNow => Now;
    }

    /// <summary>Elle ilerletilen zamanlayıcı. DelayAsync hemen tamamlanır ama istenen süreyi kaydeder.</summary>
    public sealed class FakeScheduler : IScheduler
    {
        private sealed class Entry : IDisposable
        {
            public double At;
            public double Interval;
            public Action Action;
            public bool Cancelled;
            public void Dispose() => Cancelled = true;
        }

        private readonly List<Entry> entries = new List<Entry>();
        public readonly List<double> Delays = new List<double>();
        public double NowSeconds { get; private set; }

        public Task DelayAsync(double seconds)
        {
            Delays.Add(seconds);
            return Task.CompletedTask;
        }

        public IDisposable After(double seconds, Action action)
        {
            var e = new Entry { At = NowSeconds + seconds, Action = action };
            entries.Add(e);
            return e;
        }

        public IDisposable Every(double seconds, Action action)
        {
            var e = new Entry { At = NowSeconds + seconds, Interval = seconds, Action = action };
            entries.Add(e);
            return e;
        }

        public void Advance(double seconds)
        {
            double target = NowSeconds + seconds;
            while (true)
            {
                Entry next = null;
                foreach (Entry e in entries)
                {
                    if (!e.Cancelled && e.At <= target && (next == null || e.At < next.At)) next = e;
                }
                if (next == null) break;
                NowSeconds = Math.Max(NowSeconds, next.At);
                if (next.Interval > 0) next.At += next.Interval;
                else next.Cancelled = true;
                next.Action();
            }
            NowSeconds = target;
            entries.RemoveAll(e => e.Cancelled);
        }

        public int PendingCount()
        {
            int n = 0;
            foreach (Entry e in entries)
            {
                if (!e.Cancelled) n++;
            }
            return n;
        }
    }

    public sealed class FakeHttp : IHttpTransport
    {
        public readonly List<HttpRequestSpec> Requests = new List<HttpRequestSpec>();
        public Func<HttpRequestSpec, HttpResponse> Handler = spec => HttpResponse.Network();

        public Task<HttpResponse> SendAsync(HttpRequestSpec spec)
        {
            Requests.Add(spec);
            return Task.FromResult(Handler(spec));
        }

        public static HttpResponse Ok(string body) => new HttpResponse { StatusCode = 200, Body = body };
        public static HttpResponse Status(int code, string body = "{}") => new HttpResponse { StatusCode = code, Body = body };
        public static HttpResponse Error(int status, string code) => new HttpResponse { StatusCode = status, Body = "{\"code\":\"" + code + "\"}" };
    }

    public sealed class FakeWebSocket : IWebSocketTransport
    {
        public readonly List<string> Sent = new List<string>();
        public int ConnectCalls;
        public int CloseCalls;
        public bool IsOpen { get; private set; }

        public event Action Opened;
        public event Action<string> MessageReceived;
        public event Action<int> Closed;

        public void Connect(string url) => ConnectCalls++;
        public void Send(string text) => Sent.Add(text);

        public void Close()
        {
            CloseCalls++;
            if (IsOpen)
            {
                IsOpen = false;
                Closed?.Invoke(1000);
            }
        }

        // --- Test denetimi ---
        public void SimulateOpen()
        {
            IsOpen = true;
            Opened?.Invoke();
        }

        public void SimulateMessage(string json) => MessageReceived?.Invoke(json);

        public void SimulateClose(int code)
        {
            IsOpen = false;
            Closed?.Invoke(code);
        }
    }
}
