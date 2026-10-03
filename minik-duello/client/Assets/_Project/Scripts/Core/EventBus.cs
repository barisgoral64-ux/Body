using System;
using System.Collections.Generic;

namespace MinikDuello.Core
{
    /// <summary>Gevşek bağlı olay yolu (UI güncellemeleri için). Dinleyici hatası diğerlerini durdurmaz.</summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, List<Delegate>> Handlers = new Dictionary<Type, List<Delegate>>();

        public static void Subscribe<T>(Action<T> handler)
        {
            Type type = typeof(T);
            if (!Handlers.TryGetValue(type, out List<Delegate> list))
            {
                list = new List<Delegate>();
                Handlers[type] = list;
            }
            list.Add(handler);
        }

        public static void Unsubscribe<T>(Action<T> handler)
        {
            if (Handlers.TryGetValue(typeof(T), out List<Delegate> list)) list.Remove(handler);
        }

        public static void Publish<T>(T message)
        {
            if (!Handlers.TryGetValue(typeof(T), out List<Delegate> list)) return;
            // Kopya üzerinde dolaşılır: dinleyici kendini çıkarabilir.
            foreach (Delegate handler in list.ToArray())
            {
                try
                {
                    ((Action<T>)handler)(message);
                }
                catch (Exception exception)
                {
                    Log.Error("EventBus", exception);
                }
            }
        }

        public static void Clear() => Handlers.Clear();
    }
}
