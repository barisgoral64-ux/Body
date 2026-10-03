using System;

namespace MinikDuello.Core
{
    public enum LogLevel
    {
        Debug = 10,
        Info = 20,
        Warn = 30,
        Error = 40
    }

    /// <summary>Log çıktı hedefi. Unity'de UnityLogSink, testte bellek içi sink kullanılır.</summary>
    public interface ILogSink
    {
        void Write(LogLevel level, string line);
    }

    public sealed class ConsoleLogSink : ILogSink
    {
        public void Write(LogLevel level, string line) => Console.WriteLine("[" + level + "] " + line);
    }

    /// <summary>Seviyeli log. Üretimde Debug kapalıdır; token/PIN asla loglanmaz.</summary>
    public static class Log
    {
        private static LogLevel minLevel = LogLevel.Debug;
        private static ILogSink sink = new ConsoleLogSink();

        public static void Configure(LogLevel level, ILogSink newSink = null)
        {
            minLevel = level;
            if (newSink != null) sink = newSink;
        }

        public static void Debug(string scope, string message) => Write(LogLevel.Debug, scope, message);
        public static void Info(string scope, string message) => Write(LogLevel.Info, scope, message);
        public static void Warn(string scope, string message) => Write(LogLevel.Warn, scope, message);
        public static void Error(string scope, string message) => Write(LogLevel.Error, scope, message);

        public static void Error(string scope, Exception exception) =>
            Write(LogLevel.Error, scope, exception.GetType().Name + ": " + exception.Message);

        private static void Write(LogLevel level, string scope, string message)
        {
            if (level < minLevel) return;
            sink.Write(level, "[" + scope + "] " + message);
        }
    }
}
