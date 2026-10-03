using System;

namespace MinikDuello.Services
{
    /// <summary>
    /// Son ağ isteklerinin sonucuna göre "çevrimiçi mi" bilgisi ve "sunucu bu sürümü artık desteklemiyor" işareti.
    /// </summary>
    public sealed class ConnectivityState
    {
        public bool IsOnline { get; private set; } = true;
        public bool UpdateRequired { get; private set; }
        public event Action<bool> Changed;
        public event Action UpdateRequiredDetected;

        public void Report(bool reachable)
        {
            if (IsOnline == reachable) return;
            IsOnline = reachable;
            Changed?.Invoke(reachable);
        }

        public void ReportUpdateRequired()
        {
            if (UpdateRequired) return;
            UpdateRequired = true;
            UpdateRequiredDetected?.Invoke();
        }
    }
}
