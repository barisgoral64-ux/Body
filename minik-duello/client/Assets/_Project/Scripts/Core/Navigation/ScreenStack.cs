using System;
using System.Collections.Generic;

namespace MinikDuello.UI
{
    /// <summary>
    /// Ekran gezinme yığını (saf C#). Kök ekran asla çıkarılmaz; böylece "geri" çocuğu boş ekrana düşürmez.
    /// </summary>
    public sealed class ScreenStack<T> where T : struct
    {
        private readonly List<T> items = new List<T>();

        public int Count => items.Count;
        public bool CanGoBack => items.Count > 1;

        public T Current
        {
            get
            {
                if (items.Count == 0) throw new InvalidOperationException("Yığın boş.");
                return items[items.Count - 1];
            }
        }

        public void Reset(T root)
        {
            items.Clear();
            items.Add(root);
        }

        public void Push(T item)
        {
            // Aynı ekrana art arda çift dokunuşla iki kez gitmeyi engeller.
            if (items.Count > 0 && EqualityComparer<T>.Default.Equals(Current, item)) return;
            items.Add(item);
        }

        public bool Pop(out T previous)
        {
            previous = default;
            if (!CanGoBack) return false;
            items.RemoveAt(items.Count - 1);
            previous = Current;
            return true;
        }

        public void ReplaceTop(T item)
        {
            if (items.Count == 0) throw new InvalidOperationException("Yığın boş.");
            items[items.Count - 1] = item;
        }
    }
}
