using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class NPCMovementSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        Entities
            .WithAll<NPCMovementComponent>()
            .ForEach((ref LocalTransform localTransform, ref NPCMovementComponent movement) =>
            {
                if (movement.HasTarget)
                {
                    float3 currentPosition = new float3(localTransform.Position.x, 0, localTransform.Position.z);
                    float3 targetPosition = new float3(movement.TargetPosition.x, 0, movement.TargetPosition.z);
                    float3 direction = math.normalize(targetPosition - currentPosition);

                    // Целевой угол и поворот
                    float targetAngle = math.atan2(direction.x, direction.z);
                    quaternion targetRotation = quaternion.Euler(0, targetAngle, 0);

                    // Плавно поворачиваем NPC
                    localTransform.Rotation = math.slerp(localTransform.Rotation, targetRotation, 1.3f * deltaTime);

                    // Получаем forward-вектор из текущей ориентации
                    float3 forward = math.forward(localTransform.Rotation);

                    // Угол между forward-вектором и направлением к цели
                    float angle = math.degrees(math.acos(math.clamp(math.dot(forward, direction), -1f, 1f)));

                    // Двигаемся, если угол почти 0 (т.е. смотрим на цель)
                    if (angle < 5f)
                    {
                        localTransform.Position += direction * movement.Speed * deltaTime;
                    }

                    // Останавливаемся, если близко к цели
                    if (math.distance(currentPosition, targetPosition) <= 4.2f)
                    {
                        movement.HasTarget = false;
                    }
                }
            }).ScheduleParallel();
    }
}
