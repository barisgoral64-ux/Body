using System;
using UnityEngine;

namespace MinikDuello.Core
{
    public enum LogLevel
    {
        Debug = 10,
        Info = 20,
        Warn = 30,
        Error = 40
    }

    /// <summary>Seviyeli log. Üretimde Debug kapalıdır; token/PIN asla loglanmaz.</summary>
    public static class Log
    {
        private static LogLevel minLevel = LogLevel.Debug;

        public static void Configure(LogLevel level) => minLevel = level;

        public static void Debug(string scope, string message) => Write(LogLevel.Debug, scope, message);
        public static void Info(string scope, string message) => Write(LogLevel.Info, scope, message);
        public static void Warn(string scope, string message) => Write(LogLevel.Warn, scope, message);
        public static void Error(string scope, string message) => Write(LogLevel.Error, scope, message);

        public static void Error(string scope, Exception exception) =>
            Write(LogLevel.Error, scope, exception.GetType().Name + ": " + exception.Message);

        private static void Write(LogLevel level, string scope, string message)
        {
            if (level < minLevel) return;
            string line = "[" + scope + "] " + message;
            switch (level)
            {
                case LogLevel.Error: UnityEngine.Debug.LogError(line); break;
                case LogLevel.Warn: UnityEngine.Debug.LogWarning(line); break;
                default: UnityEngine.Debug.Log(line); break;
            }
        }
    }
}
