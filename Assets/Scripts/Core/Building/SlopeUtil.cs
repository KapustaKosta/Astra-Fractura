﻿using Unity.Mathematics;
using static Unity.Mathematics.math;

public static class SlopeUtil
{
    /// <summary>
    /// true, если поверхность с нормалью n допустима при ограничении maxAngleDeg (в градусах).
    /// 0° = только идеально ровно; 90° = любая поверхность.
    /// Явно запрещает строительство на перевернутых поверхностях (нормаль направлена вниз).
    /// </summary>
    public static bool IsSlopeAllowed(float3 n, float maxAngleDeg)
    {
        n = normalize(n);
        float dotUp = dot(n, up()); 

        // Если нормаль направлена вниз (или очень близко к горизонтали, но чуть вниз), запрещаем.
        // Используем небольшой порог для устойчивости.
        if (dotUp < 0.001f) // Нормаль должна быть направлена хотя бы чуть-чуть вверх
        {
            return false;
        }

        // Вычисляем косинус максимального допустимого угла.
        // clamp maxAngleDeg to [0, 90] to ensure cos is in [0, 1].
        float cosThreshold = cos(radians(clamp(maxAngleDeg, 0f, 90f)));
        
        // Для допустимого наклона, dotUp (cos(фактического_угла)) должен быть больше или равен
        // cosThreshold (cos(максимального_разрешенного_угла)).
        // Добавим небольшой эпсилон (0.001f) к dotUp для толерантности к ошибкам с плавающей запятой.
        return dotUp + 0.001f >= cosThreshold;
    }

    /// <summary>
    /// Вычисляет угол наклона поверхности в градусах (диапазон 0-90).
    /// Возвращает 90.0f, если нормаль направлена вниз (перевернутая поверхность).
    /// </summary>
    public static float SlopeAngleDeg(float3 n)
    {
        n = normalize(n);
        float dotUp = dot(n, up());

        // Если нормаль направлена вниз, считаем максимальный наклон (90 градусов).
        if (dotUp < 0f)
        {
            return 90.0f;
        }

        // Clamp dotUp to [0, 1] for acos to avoid floating point errors
        // that could result in values slightly > 1 or < 0 for near-flat surfaces.
        return degrees(acos(clamp(dotUp, 0f, 1f)));
    }
}