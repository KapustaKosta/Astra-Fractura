using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Система-мост между ИИ-логикой и системой поиска пути.
/// Преобразует цели ИИ в запросы на перемещение для навигационной системы.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(HarvestGoalExecutionSystem))]
[UpdateAfter(typeof(ReturnToBaseGoalExecutionSystem))]
[UpdateBefore(typeof(NPCPathfindingSystem))]
public partial class AIPathfindingBridgeSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Получаем буфер команд для изменения сущностей
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

        var arrivalLookup = SystemAPI.GetComponentLookup<ArrivalData>(true);

        // Обрабатываем всех ИИ-агентов, исключая тех, кто занят сбором ресурсов
        Entities
            .WithReadOnly(arrivalLookup)
            .WithAll<NPCBrain>()
            .WithNone<WantsToHarvestTag, InsideBuildingTag>()                                       
            .ForEach((Entity entity,
                      ref NPCMovementComponent movement,
                      ref NPCPathfindingComponent pathfinding,
                      in ActiveGoal goal,
                      in NPCBaseMovementStats baseStats) =>
            {
                // Если цель уже достигается и параметры совпадают - ничего не делаем
                if (goal.Target == pathfinding.CurrentGoalTarget && movement.HasTarget)
                {
                    return;
                }

                // Обработка отсутствующей цели
                if (goal.Target == Entity.Null)
                {
                    if (movement.HasTarget)
                    {
                        Debug.Log($"[AIBridge] {entity.Index}: цель снята -> стоп движение.");
                        movement.HasTarget = false;
                        pathfinding.CurrentGoalTarget = Entity.Null;
                    }
                    return;
                }

                if (!SystemAPI.HasComponent<LocalToWorld>(goal.Target))
                    return;

                var tltw = SystemAPI.GetComponent<LocalToWorld>(goal.Target);

                float3 desired = tltw.Position;
                float stopping = math.max(0.1f, baseStats.StoppingDistance);

                if (arrivalLookup.HasComponent(goal.Target))
                {
                    var arr = arrivalLookup[goal.Target];
                    float3 off = math.mul(tltw.Rotation, arr.Offset);
                    desired = tltw.Position + off;
                    stopping = math.max(stopping, arr.Radius);
                }

                Vector3 finalPos = desired;
                float searchRadius = math.max(stopping, 1f) * 1.5f;

                if (NavMesh.SamplePosition(finalPos, out NavMeshHit hit, searchRadius, NavMesh.AllAreas))
                {
                    finalPos = hit.position;
                    if (NavMesh.FindClosestEdge(finalPos, out NavMeshHit edgeHit, NavMesh.AllAreas))
                        finalPos = edgeHit.position;
                }
                else
                {
                    Debug.LogWarning($"[AIBridge] {entity.Index}: цель {desired} не проецируется на NavMesh (r={searchRadius}).");
                    return;
                }

                Debug.Log($"[AIBridge] {entity.Index}: goal={goal.Target.Index}, desired={desired:F2} -> final={finalPos:F2}, stop={stopping:F2}");

                movement.TargetPosition = finalPos;
                movement.StoppingDistance = stopping;
                movement.HasTarget = true;

                pathfinding.NeedsPathUpdate = true;
                pathfinding.CurrentWaypointIndex = 0;
                pathfinding.CurrentGoalTarget = goal.Target;

                Debug.DrawLine(tltw.Position, finalPos, Color.magenta, 2f);

                if (!SystemAPI.HasComponent<MoveToRequest>(entity))
                {
                    ecb.AddComponent(entity, new MoveToRequest
                    {
                        TargetEntity = goal.Target,
                        StoppingDistance = stopping
                    });
                }
                else
                {
                    var req = SystemAPI.GetComponentRW<MoveToRequest>(entity);
                    req.ValueRW.TargetEntity = goal.Target;
                    req.ValueRW.StoppingDistance = stopping;
                }
            })
            .Run();
    }
}