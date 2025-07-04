using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;

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
            .ForEach((ref LocalTransform localTransform, ref PhysicsVelocity physicsVelocity, ref NPCMovementComponent movement) =>
            {
                if (movement.HasTarget)
                {
                    float3 currentPositionXZ = new float3(localTransform.Position.x, 0, localTransform.Position.z);
                    float3 targetPositionXZ = new float3(movement.TargetPosition.x, 0, movement.TargetPosition.z);
                    
                    if (math.distance(currentPositionXZ, targetPositionXZ) > 0.5f)
                    {
                        float3 direction = math.normalize(targetPositionXZ - currentPositionXZ);

                        float targetAngle = math.atan2(direction.x, direction.z);
                        quaternion targetRotation = quaternion.Euler(0, targetAngle, 0);
                        localTransform.Rotation = math.slerp(localTransform.Rotation, targetRotation, 5.0f * deltaTime);

                        physicsVelocity.Linear = direction * movement.Speed;
                        physicsVelocity.Angular = float3.zero;

                    }
                    else
                    {
                        physicsVelocity.Linear = float3.zero;
                        movement.HasTarget = false;
                    }
                }
                else
                {
                    if (math.lengthsq(physicsVelocity.Linear) > 0.001f)
                    {
                         physicsVelocity.Linear = float3.zero;
                    }
                }
            }).ScheduleParallel();
    }
}