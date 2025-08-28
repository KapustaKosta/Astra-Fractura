using Unity.Mathematics;

namespace Energy.Core
{
    public static class Units
    {
        public const float W_to_kW = 1f / 1000f;
        public const float kW_to_W = 1000f;
        public const float J_to_kWh = 1f / 3_600_000f;
        public const float kWh_to_J = 3_600_000f;

        public static float SafeDivide(float num, float den, float fallback = 0f)
            => math.abs(den) > 1e-6f ? num / den : fallback;
    }
}