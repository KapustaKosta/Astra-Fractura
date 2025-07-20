using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Collections;

/// <summary>
/// Система физического движения NPC.
/// Управляет перемещением NPC по заданным маршрутам через обновление физической скорости.
/// Интегрируется с системой поиска пути и ИИ для реализации навигации.
/// </summary>
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[RequireMatchingQueriesForUpdate]
public partial class NPCMovementSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        // Обрабатываем всех сущностей с компонентами движения и физики
        Entities
            .WithAll<NPCMovementComponent>()
            .WithAll<PhysicsVelocity>()
            .ForEach((Entity entity, ref LocalTransform localTransform, 
                     ref PhysicsVelocity physicsVelocity, ref NPCMovementComponent movement, 
                     ref NPCPathfindingComponent pathfinding, 
                     in DynamicBuffer<NPCPathBufferElement> pathBuffer) =>
            {
                // Основная логика движения:
                if (movement.HasTarget && pathBuffer.Length > 0 && 
                    pathfinding.CurrentWaypointIndex < pathBuffer.Length)
                {
                    // Получаем текущую путевую точку
                    float3 currentWaypoint = pathBuffer[pathfinding.CurrentWaypointIndex].Waypoint;
                    
                    // Используем только X и Z координаты для 2D-движения
                    float3 currentPositionXZ = new float3(localTransform.Position.x, 0, localTransform.Position.z);
                    float3 waypointXZ = new float3(currentWaypoint.x, 0, currentWaypoint.z);

                    // Вычисляем расстояние до точки
                    float dist = math.distance(currentPositionXZ, waypointXZ);
                    
                    // Если не достигли точки остановки
                    if (dist > movement.StoppingDistance)
                    {
                        // Вычисляем направление и поворот
                        float3 direction = math.normalize(waypointXZ - currentPositionXZ);
                        float targetAngle = math.atan2(direction.x, direction.z);
                        quaternion targetRotation = quaternion.Euler(0, targetAngle, 0);
                        
                        // Плавный поворот к цели
                        localTransform.Rotation = math.slerp(
                            localTransform.Rotation, 
                            targetRotation, 
                            movement.RotationSpeed * deltaTime);

                        // Устанавливаем линейную скорость
                        physicsVelocity.Linear = direction * movement.Speed;
                        physicsVelocity.Angular = float3.zero; // Отключаем вращение
                        
                        UnityEngine.Debug.Log($"[NPCMovementSystem] Entity {entity.Index}: Двигается к точке {pathfinding.CurrentWaypointIndex} (dist={dist:F2})");
                    }
                    else
                    {
                        // Переход к следующей путевой точке
                        pathfinding.CurrentWaypointIndex++;
                        UnityEngine.Debug.Log($"[NPCMovementSystem] Entity {entity.Index}: Достиг точки {pathfinding.CurrentWaypointIndex - 1}, переход к следующей");

                        // Проверка завершения маршрута
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
                    // Очистка скорости если нет цели или путь недоступен
                    if (math.lengthsq(physicsVelocity.Linear) > movement.VelocityZeroingThresholdSq)
                    {
                        physicsVelocity.Linear = float3.zero;
                    }
                }
            }).ScheduleParallel();
    }
}