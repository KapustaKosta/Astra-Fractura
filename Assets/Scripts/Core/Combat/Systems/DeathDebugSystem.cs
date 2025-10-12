// Assets/Scripts/Core/Combat/Systems/DeathDebugSystem.cs

﻿using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using UnityEngine;

// Подробный лог поз/углов/скоростей после смерти, каждые kTraceInterval секунд.
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(DeathSystem))]
public partial struct DeathDebugSystem : ISystem
{
    const float kTraceInterval = 0.21f; // частота логирования

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<DeathTrace>();
    }

    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // Ведём тайминг через компонент-счётчик на каждой сущности
        foreach (var (traceRW, timerRW, lt, l2w, entity) in
                 SystemAPI.Query<RefRW<DeathTrace>, RefRW<DeathTimer>, RefRO<LocalTransform>, RefRO<LocalToWorld>>()
                          .WithEntityAccess())
        {
            ref var trace = ref traceRW.ValueRW;
            ref var timer = ref timerRW.ValueRW;

            // Храним внутренний дробный акк, используя Elapsed (не создавая отдельный компонент)
            float nextMark = math.floor((trace.Elapsed + 1e-5f) / kTraceInterval) * kTraceInterval;
            float prevMark = math.floor((trace.Elapsed - dt) / kTraceInterval) * kTraceInterval;

            if (nextMark > prevMark) // время логировать
            {
                float3 pos = l2w.ValueRO.Position;
                float3 euler = new float3x3(l2w.ValueRO.Rotation).Euler();
                // Для людей: округлённый короткий лог
                Debug.Log($"[DeathTracePos] e={entity.Index} t={trace.Elapsed:0.00}s Pos=({pos.x:0.###},{pos.y:0.###},{pos.z:0.###})");

                // Подробный лог
                float3 forward = math.mul(l2w.ValueRO.Rotation, new float3(0,0,1));
                float3 hitXZ   = trace.HitDirXZ;
                float3 awayXZ  = hitXZ; // это «от удара» в плоскости XZ

                // Попробуем вытащить скорости, если есть
                float3 lin = float3.zero;
                float3 ang = float3.zero;
                if (SystemAPI.HasComponent<PhysicsVelocity>(entity))
                {
                    var pv = SystemAPI.GetComponent<PhysicsVelocity>(entity);
                    lin = pv.Linear; ang = pv.Angular;
                }

                Debug.Log($"[DeathTrace] e={entity.Index} t={trace.Elapsed:0.00}s | Pos=float3({pos.x:0.#####}f, {pos.y:0.#####}f, {pos.z:0.#####}f) | " +
                          $"EulerXYZ°=({euler.x:0.0},{euler.y:0.0},{euler.z:0.0}) | fwd=float3({forward.x:0.######}f, {forward.y:0.######}f, {forward.z:0.######}f) | " +
                          $"HitXZ=float3({hitXZ.x:0.######}f, 0f, {hitXZ.z:0.######}f) AwayXZ=float3({awayXZ.x:0.######}f, 0f, {awayXZ.z:0.######}f) | " +
                          $"PV.lin=({lin.x:0.##},{lin.y:0.##},{lin.z:0.##}) PV.ang=({ang.x:0.##},{ang.y:0.##},{ang.z:0.##})");
            }

            // После завершения трейса — убираем компонент, чтобы не спамить
            if (trace.Elapsed >= trace.Duration)
            {
                ecb.RemoveComponent<DeathTrace>(entity);
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}

// Небольшой helper для получения эйлеров из rotation matrix
static class DeathDebugMath
{
    // Возвращает XYZ-эйлеры из m (в градусах)
    public static float3 Euler(this float3x3 m)
    {
        // стандартная конверсия
        float sy = math.sqrt(m.c0.x * m.c0.x + m.c1.x * m.c1.x);
        bool singular = sy < 1e-6f;
        float x, y, z;
        if (!singular)
        {
            x = math.degrees(math.atan2(m.c2.y, m.c2.z));
            y = math.degrees(math.atan2(-m.c2.x, sy));
            // ИСПРАВЛЕНО: c1.x -> m.c1.x
            z = math.degrees(math.atan2(m.c1.x, m.c0.x));
        }
        else
        {
            x = math.degrees(math.atan2(-m.c1.z, m.c1.y));
            y = math.degrees(math.atan2(-m.c2.x, sy));
            z = 0;
        }
        return new float3(x, y, z);
    }
}