using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Система, отвечающая за следование NPC по пути, заранее построенному системой NPCPathfindingSystem.
/// Она вычисляет "предпочтительную скорость" (PreferredVelocity), направленную к текущей
/// путевой точке (waypoint), реализует логику пропуска близких точек, "заглядывания вперед"
/// на прямых участках и плавного замедления у конечной цели.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(NPCPathfindingSystem))]
[UpdateBefore(typeof(NPCLocalAvoidanceSystem))]
public partial class NPCPathFollowSystem : SystemBase
{
    private const float WaypointReachEps      = 0.35f;
    private const float LookAheadCosThreshold = 0.97f;
    private const float MinimumCruiseSpeed     = 1.75f;
    private const float SlowDownRadiusFactor   = 1.6f;
    private const float MinSpeedHold           = 0.35f;
    
    /// <summary>
    /// Основной метод обновления системы, выполняемый каждый кадр.
    /// Запускает выполнение логики следования по пути для всех сущностей с необходимыми компонентами.
    /// </summary>
    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;

        Entities
        .WithName("NPCPathFollow_WithLookAhead_AndSpeedFloor")
        .ForEach((Entity e,
                  ref NPCMovementComponent movement,
                  ref NPCPathfindingComponent path,
                  in LocalTransform lt,
                  in DynamicBuffer<NPCPathBufferElement> corners) =>
        {
            /// <summary>
            /// Основная логика, выполняемая для каждой отдельной сущности.
            /// Если путь пуст, движение останавливается. В противном случае, система определяет
            /// текущую целевую точку из буфера пути, пропуская уже достигнутые.
            /// Реализован механизм "look-ahead", позволяющий целиться на следующую точку, если
            /// текущий и следующий сегменты пути почти коллинеарны. Рассчитывается желаемая
            /// скорость с учетом замедления у финальной точки и устанавливается в
            /// `movement.PreferredVelocity`.
            /// </summary>
            if (corners.Length == 0)
            {
                movement.PreferredVelocity = float3.zero;
                movement.HasTarget = false;
                return;
            }

            int idx = math.clamp(path.CurrentWaypointIndex, 0, math.max(0, corners.Length - 1));

            float3 pos3D = lt.Position;
            float3 wp3D  = corners[idx].Waypoint;

            float2 pos2D = new float2(pos3D.x, pos3D.z);
            float2 wp2D  = new float2(wp3D.x, wp3D.z);
            float2 to2D  = wp2D - pos2D;
            float  distSq = math.lengthsq(to2D);

            while (distSq < WaypointReachEps * WaypointReachEps && idx < corners.Length - 1)
            {
                idx++;
                wp3D = corners[idx].Waypoint;
                wp2D = new float2(wp3D.x, wp3D.z);
                to2D = wp2D - pos2D;
                distSq = math.lengthsq(to2D);
            }

            if (idx < corners.Length - 2)
            {
                float3 cur = corners[idx].Waypoint;
                float3 nxt = corners[idx + 1].Waypoint;
                float3 aft = corners[idx + 2].Waypoint;

                float2 d1 = math.normalizesafe(new float2(nxt.x - cur.x, nxt.z - cur.z));
                float2 d2 = math.normalizesafe(new float2(aft.x - nxt.x, aft.z - nxt.z));

                if (math.dot(d1, d2) > LookAheadCosThreshold)
                {
                    if (math.lengthsq(new float2(aft.x - pos3D.x, aft.z - pos3D.z)) >
                        WaypointReachEps * WaypointReachEps)
                    {
                        idx += 1;
                        wp3D = corners[idx].Waypoint;
                        wp2D = new float2(wp3D.x, wp3D.z);
                        to2D = wp2D - pos2D;
                        distSq = math.lengthsq(to2D);
                    }
                }
            }

            path.CurrentWaypointIndex = idx;

            bool isLastCorner = (idx == corners.Length - 1);
            float baseStop    = math.max(0.1f, movement.StoppingDistance);
            float midStop     = math.clamp(baseStop * 0.5f, 0.15f, 0.25f);
            float activeStop  = isLastCorner ? baseStop : midStop;

            float dist = math.sqrt(math.max(distSq, 0f));
            float slowZone = math.clamp(baseStop * SlowDownRadiusFactor, 0.2f, 3.5f);

            float3 vPref;
            if (dist > 1e-4f)
            {
                float3 dir = new float3(to2D.x, 0, to2D.y) / dist;
                float desiredSpeed = math.max(movement.Speed, 0.1f);
                if (isLastCorner)
                {
                    float k = math.saturate(dist / slowZone);
                    desiredSpeed *= k;
                }
                
                float minHold = desiredSpeed * MinSpeedHold;
                float2 curXZ  = new float2(movement.PreferredVelocity.x, movement.PreferredVelocity.z);
                if (math.length(curXZ) < minHold)
                    desiredSpeed = math.max(desiredSpeed, minHold);

                vPref = dir * desiredSpeed;
            }
            else
            {
                vPref = float3.zero;
            }

            if (isLastCorner && dist <= activeStop)
            {
                vPref = float3.zero;
                movement.HasTarget = false;
            }
            else
            {
                movement.HasTarget = true;
            }

            movement.TargetPosition    = wp3D;
            movement.StoppingDistance  = activeStop;
            movement.PreferredVelocity = vPref;
        })
        .WithoutBurst()
        .Run();
    }
}