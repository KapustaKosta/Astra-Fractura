using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Система, служащая мостом между логикой искусственного интеллекта (ИИ) и системой поиска пути.
/// Преобразует цели ИИ в запросы на перемещение для навигационной системы.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ReturnToBaseGoalExecutionSystem))]
[UpdateBefore(typeof(NPCPathfindingSystem))]
public partial class AIPathfindingBridgeSystem : SystemBase
{
    private const float StaticTargetRetargetDistanceSq  = 0.75f * 0.75f;
    private const float DynamicTargetRetargetDistanceSq = 0.10f * 0.10f;

    /// <summary>
    /// Основной метод обновления системы, выполняемый каждый кадр.
    /// Он подготавливает необходимые данные и запускает итерацию по всем сущностям,
    /// которые нуждаются в обновлении пути.
    /// </summary>
    protected override void OnUpdate()
    {
        var arrivalLookup    = SystemAPI.GetComponentLookup<ArrivalData>(true);
        var ltwLookup        = SystemAPI.GetComponentLookup<LocalToWorld>(true);
        var playerTagLookup  = SystemAPI.GetComponentLookup<PlayerTag>(true);
        var hostileTagLookup = SystemAPI.GetComponentLookup<HostileNPCTag>(true);

        var ecb = SystemAPI
            .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

        Entities
            .WithReadOnly(arrivalLookup)
            .WithReadOnly(ltwLookup)
            .WithReadOnly(playerTagLookup)
            .WithReadOnly(hostileTagLookup)
            .WithAny<NPCBrain, HostileNPCTag>()
            .WithNone<IsDeadTag>()
            .WithNone<WantsToHarvestTag, InsideBuildingTag, IsAttackingTag>()
            .ForEach((Entity e,
                      ref NPCMovementComponent movement,
                      ref NPCPathfindingComponent path,
                      in ActiveGoal goal,
                      in NPCBaseMovementStats baseStats) =>
            {
                if (goal.Target == Entity.Null || !ltwLookup.HasComponent(goal.Target))
                {
                    if (movement.HasTarget) movement.HasTarget = false;
                    return;
                }

                var targetLtw         = ltwLookup[goal.Target];
                bool isTargetDynamic  = playerTagLookup.HasComponent(goal.Target);
                bool isHostile        = hostileTagLookup.HasComponent(e);

                float3 centralTargetPos = targetLtw.Position;
                float   stoppingRadius   = baseStats.StoppingDistance;

                float distributionRadius = 1.5f;  

                bool hasArrivalData = arrivalLookup.HasComponent(goal.Target);
                if (hasArrivalData)
                {
                    var arrivalData = arrivalLookup[goal.Target];
                    centralTargetPos += math.mul(targetLtw.Rotation, arrivalData.Offset);
                    stoppingRadius    = math.max(stoppingRadius, arrivalData.Radius);
                    distributionRadius = arrivalData.Radius;  
                }

                float3 finalDesiredPos = centralTargetPos;

                if (!isHostile && !isTargetDynamic)
                {
                    uint hash = (uint)e.Index * 2654435761u;
                    float angle = (hash % 360) * math.PI / 180f;
                    float3 offsetDir = math.normalizesafe(new float3(math.cos(angle), 0, math.sin(angle)));

                    // Если у цели есть ArrivalData, то это либо ресурс для добычи, либо точка
                    // на базе/укрытии.
                    if (hasArrivalData)
                    {
                        if (goal.Type == GoalType.Harvest)
                        {
                            // Используем фиксированную длину — разместить на границе радиуса
                            finalDesiredPos += offsetDir * distributionRadius;
                        }
                        else
                        {
                            // Для других Arrival целей варьируем радиус от 60% до 100%
                            uint h2  = (uint)e.Index * 1664525u + 1013904223u;
                            float u  = (float)(h2 & 0x00FFFFFFu) / 16777216f; // [0,1)
                            float radiusFactor = math.lerp(0.6f, 1.0f, u);
                            finalDesiredPos += offsetDir * (distributionRadius * radiusFactor);
                        }
                    }
                    else
                    {
                        // Для целей без ArrivalData оставляем лёгкую вариацию радиуса (0.8–1.2)
                        float randomFactor = 0.8f + (hash % 1000) / 1000f * 0.4f;  // 0.8-1.2 для вариации
                        finalDesiredPos += offsetDir * (distributionRadius * randomFactor);
                    }
                }

                bool  isSameTarget        = goal.Target == path.CurrentGoalTarget;
                float distSqToLastTarget  = math.distancesq(finalDesiredPos, path.LastTargetPosition);
                float retargetThresholdSq = isTargetDynamic ? DynamicTargetRetargetDistanceSq : StaticTargetRetargetDistanceSq;

                if (isSameTarget && movement.HasTarget && distSqToLastTarget < retargetThresholdSq)
                {
                    return;
                }

                
                if (NavMesh.SamplePosition(finalDesiredPos, out var hit, 10.0f, NavMesh.AllAreas))
                {
                    movement.TargetPosition   = hit.position;
                    movement.StoppingDistance = stoppingRadius;
                    movement.HasTarget        = true;

                    path.NeedsPathUpdate      = true;
                    path.CurrentWaypointIndex = 0;
                    path.CurrentGoalTarget    = goal.Target;
                    path.LastTargetPosition   = finalDesiredPos;

                    if (SystemAPI.HasComponent<MovementFailedTag>(e))
                        ecb.RemoveComponent<MovementFailedTag>(e);
                }
                else
                {
                    if (!SystemAPI.HasComponent<MovementFailedTag>(e))
                        ecb.AddComponent<MovementFailedTag>(e);
                }
            })
            .WithoutBurst()
            .Run();
    }
}