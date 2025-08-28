using Unity.Mathematics;
using static Unity.Mathematics.math;

public static class SlopeUtil
{
    /// <summary>
    /// true, если поверхность с нормалью n допустима при ограничении maxAngleDeg (в градусах).
    /// 0° = только идеально ровно; 90° = любая поверхность.
    /// Нечувствительно к направлению нормали (используется abs(dot)).
    /// </summary>
    public static bool IsSlopeAllowed(float3 n, float maxAngleDeg)
    {
        n = normalize(n);
        float cosThreshold = cos(radians(clamp(maxAngleDeg, 0f, 90f)));
        float d = abs(dot(n, up())); // защищаемся от перевёрнутых треугольников
        return d + 1e-4f >= cosThreshold;
    }

    public static float SlopeAngleDeg(float3 n)
    {
        n = normalize(n);
        float d = clamp(dot(n, up()), -1f, 1f);
        return degrees(acos(d));
    }
}