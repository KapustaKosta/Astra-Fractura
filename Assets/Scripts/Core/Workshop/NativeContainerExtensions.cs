using Unity.Collections;
using Unity.Entities;

// Мы помещаем его в тот же namespace, чтобы другие системы цеха его "видели" автоматически
namespace Game.Workshop
{
    /// <summary>
    /// Содержит полезные методы-расширения для нативных контейнеров, таких как NativeHashMap.
    /// </summary>
    public static class NativeContainerExtensions
    {
        /// <summary>
        /// Увеличивает значение по ключу. Если ключ не существует, он создается со значением value.
        /// </summary>
        public static void Increment<TKey>(this NativeHashMap<TKey, int> map, TKey key, int value) where TKey : unmanaged, System.IEquatable<TKey>
        {
            if (map.TryGetValue(key, out int current))
                map[key] = current + value;
            else
                map[key] = value;
        }

        /// <summary>
        /// Уменьшает значение по ключу.
        /// </summary>
        public static void Decrement<TKey>(this NativeHashMap<TKey, int> map, TKey key, int value) where TKey : unmanaged, System.IEquatable<TKey>
        {
            if (map.TryGetValue(key, out int current))
                map[key] = current - value;
        }
    }
}