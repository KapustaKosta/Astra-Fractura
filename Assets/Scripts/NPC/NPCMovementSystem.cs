using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Collections;

/// <summary>
/// Система ECS, управляющая движением NPC.
/// Обновляет физическую скорость NPC на основе их целевой позиции.
/// Работает в группе симуляции физики.
/// </summary>
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[RequireMatchingQueriesForUpdate]
public partial class NPCMovementSystem : SystemBase
{
    /// <summary>
    /// Вызывается каждый физический кадр.
    /// Перебирает все сущности с NPCMovementComponent, LocalTransform и PhysicsVelocity,
    /// и обновляет их скорость для перемещения к целевой позиции.
    /// </summary>
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        Entities
            .WithAll<NPCMovementComponent>()
            .WithAll<PhysicsVelocity>()
            .ForEach((Entity entity, ref LocalTransform localTransform, ref PhysicsVelocity physicsVelocity, ref NPCMovementComponent movement, ref NPCPathfindingComponent pathfinding, in DynamicBuffer<NPCPathBufferElement> pathBuffer) =>
            {
                if (movement.HasTarget && pathBuffer.Length > 0 && pathfinding.CurrentWaypointIndex < pathBuffer.Length)
                {
                    float3 currentWaypoint = pathBuffer[pathfinding.CurrentWaypointIndex].Waypoint;
                    float3 currentPositionXZ = new float3(localTransform.Position.x, 0, localTransform.Position.z);
                    float3 waypointXZ = new float3(currentWaypoint.x, 0, currentWaypoint.z);

                    float dist = math.distance(currentPositionXZ, waypointXZ);
                    if (dist > movement.StoppingDistance)
                    {
                        float3 direction = math.normalize(waypointXZ - currentPositionXZ);

                        float targetAngle = math.atan2(direction.x, direction.z);
                        quaternion targetRotation = quaternion.Euler(0, targetAngle, 0);
                        localTransform.Rotation = math.slerp(localTransform.Rotation, targetRotation, movement.RotationSpeed * deltaTime);

                        physicsVelocity.Linear = direction * movement.Speed;
                        physicsVelocity.Angular = float3.zero;

                        // DEBUG: Движение к точке
                        UnityEngine.Debug.Log($"[NPCMovementSystem] Entity {entity.Index}: Двигается к точке {pathfinding.CurrentWaypointIndex} (dist={dist:F2})");
                    }
                    else
                    {
                        pathfinding.CurrentWaypointIndex++;
                        UnityEngine.Debug.Log($"[NPCMovementSystem] Entity {entity.Index}: Достиг точки {pathfinding.CurrentWaypointIndex - 1}, переход к следующей");
                        if (pathfinding.CurrentWaypointIndex >= pathBuffer.Length)
                        {
                            physicsVelocity.Linear = float3.zero;
                            movement.HasTarget = false;
                            UnityEngine.Debug.Log($"[NPCMovementSystem] Entity {entity.Index}: Путь завершён, цель достигнута");
                        }
                    }
                }
                else
                {
                    if (math.lengthsq(physicsVelocity.Linear) > movement.VelocityZeroingThresholdSq)
                    {
                        physicsVelocity.Linear = float3.zero;
                    }
                }
            }).ScheduleParallel();
    }
}