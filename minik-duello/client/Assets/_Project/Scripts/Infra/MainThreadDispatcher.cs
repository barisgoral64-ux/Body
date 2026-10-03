using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace MinikDuello.Infra
{
    /// <summary>Arka plan iş parçacıklarından (WebSocket) gelen olayları Unity ana iş parçacığına taşır.</summary>
    public sealed class MainThreadDispatcher : MonoBehaviour
    {
        private const int MaxPerFrame = 64;
        private static readonly ConcurrentQueue<Action> Queue = new ConcurrentQueue<Action>();

        public static void Post(Action action) => Queue.Enqueue(action);

        private void Update()
        {
            int processed = 0;
            while (processed < MaxPerFrame && Queue.TryDequeue(out Action action))
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    MinikDuello.Core.Log.Error("Dispatcher", exception);
                }
                processed++;
            }
        }
    }
}
