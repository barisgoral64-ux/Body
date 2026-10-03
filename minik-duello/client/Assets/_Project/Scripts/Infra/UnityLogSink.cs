using MinikDuello.Core;
using UnityEngine;

namespace MinikDuello.Infra
{
    public sealed class UnityLogSink : ILogSink
    {
        public void Write(LogLevel level, string line)
        {
            switch (level)
            {
                case LogLevel.Error: Debug.LogError(line); break;
                case LogLevel.Warn: Debug.LogWarning(line); break;
                default: Debug.Log(line); break;
            }
        }
    }
}
