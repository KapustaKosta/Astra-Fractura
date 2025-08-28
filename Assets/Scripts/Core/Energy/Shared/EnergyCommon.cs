using Unity.Collections;

namespace Energy.Core
{
    public static class EnergyCommon
    {
        // «Сложить значение по ключу» для NativeParallelHashMap.
        public static void AddOrAccumulate(ref NativeParallelHashMap<int, float> map, int key, float value)
        {
            if (map.TryGetValue(key, out float cur))
            {
                // В NP HashMap нет индексатора/SetValue в старых версиях, делаем Remove+TryAdd.
                map.Remove(key);
                map.TryAdd(key, cur + value);
            }
            else
            {
                map.TryAdd(key, value);
            }
        }
    }
}