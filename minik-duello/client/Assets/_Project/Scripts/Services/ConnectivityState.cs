using System;

namespace MinikDuello.Services
{
    /// <summary>Son ağ isteklerinin sonucuna göre "çevrimiçi mi" bilgisi. Arayüz bununla nazik bir çevrimdışı notu gösterir.</summary>
    public sealed class ConnectivityState
    {
        public bool IsOnline { get; private set; } = true;
        public event Action<bool> Changed;

        public void Report(bool reachable)
        {
            if (IsOnline == reachable) return;
            IsOnline = reachable;
            Changed?.Invoke(reachable);
        }
    }
}
