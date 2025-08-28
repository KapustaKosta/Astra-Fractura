using Unity.Mathematics;

namespace Conveyor
{
    /// <summary>
    /// Единая логика квантизации прямого отрезка на N секций.
    /// Используется и превью, и финализацией — чтобы не расходились.
    /// </summary>
    public static class ConveyorQuantization
    {
        public static void QuantizeStraight(float len, float minLen, float maxLen, out int count, out float perLen)
        {
            count = math.max(1, (int)math.ceil(len / math.max(maxLen, 1e-4f)));
            perLen = math.clamp(len / count, minLen, maxLen);
        }
    }
}
