using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MinikDuello.Core;
using MinikDuello.Services;
using UnityEngine;

namespace MinikDuello.Infra
{
    /// <summary>Gerçek zamanlı (unscaled) zamanlayıcı: duraklatmadan etkilenmez, yalnızca ana iş parçacığında çalışır.</summary>
    public sealed class UnityScheduler : MonoBehaviour, IScheduler
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
        private readonly List<Entry> due = new List<Entry>();

        public double NowSeconds => Time.unscaledTimeAsDouble;

        public Task DelayAsync(double seconds)
        {
            var tcs = new TaskCompletionSource<bool>();
            After(seconds, () => tcs.TrySetResult(true));
            return tcs.Task;
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

        private void Update()
        {
            double now = NowSeconds;
            due.Clear();
            foreach (Entry e in entries)
            {
                if (!e.Cancelled && e.At <= now) due.Add(e);
            }
            foreach (Entry e in due)
            {
                if (e.Cancelled) continue;
                if (e.Interval > 0) e.At = now + e.Interval;
                else e.Cancelled = true;
                try
                {
                    e.Action();
                }
                catch (Exception exception)
                {
                    Log.Error("Scheduler", exception);
                }
            }
            entries.RemoveAll(x => x.Cancelled);
        }
    }
}
