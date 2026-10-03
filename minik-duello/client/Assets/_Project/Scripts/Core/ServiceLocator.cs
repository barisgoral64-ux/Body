using System;
using System.Collections.Generic;

namespace MinikDuello.Core
{
    /// <summary>
    /// Arayüz tabanlı servis kaydı. Test için Register ile sahte (mock) servis verilebilir.
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> Services = new Dictionary<Type, object>();

        public static void Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            Services[typeof(T)] = service;
        }

        public static T Get<T>() where T : class
        {
            if (Services.TryGetValue(typeof(T), out object service)) return (T)service;
            throw new InvalidOperationException("Servis kayıtlı değil: " + typeof(T).Name);
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            if (Services.TryGetValue(typeof(T), out object found))
            {
                service = (T)found;
                return true;
            }
            service = null;
            return false;
        }

        public static void Clear() => Services.Clear();
    }
}
