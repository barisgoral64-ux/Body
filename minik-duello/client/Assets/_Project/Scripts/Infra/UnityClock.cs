using System;
using MinikDuello.Services;

namespace MinikDuello.Infra
{
    public sealed class UnityClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime LocalNow => DateTime.Now;
    }
}
