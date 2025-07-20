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
[UpdateAfter(typeof(HarvestGoalExecutionSystem))] // После обработки целей сбора
[UpdateAfter(typeof(ReturnToBaseGoalExecutionSystem))] // После возврата к базе
[UpdateBefore(typeof(NPCPathfindingSystem))] // Перед выполнением поиска пути
public partial class AIPathfindingBridgeSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Получаем буфер команд для изменения сущностей
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);
        
        // Получаем глобальные настройки ИИ
        var settings = SystemAPI.GetSingleton<AISettings>();

        // Обрабатываем всех ИИ-агентов, исключая тех, кто занят сбором ресурсов
        Entities
            .WithAll<NPCBrain>() // Только сущности с мозгом ИИ
            .WithNone<WantsToHarvestTag>() // Исключаем NPC в режиме сбора
            .ForEach((Entity entity, ref NPCMovementComponent movement, 
                     ref NPCPathfindingComponent pathfinding, in ActiveGoal goal, 
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
                        // Сбрасываем параметры движения
                        movement.HasTarget = false;
                        pathfinding.CurrentGoalTarget = Entity.Null;
                    }
                    return;
                }

                // Проверяем доступность целевой позиции
                if (!SystemAPI.HasComponent<LocalToWorld>(goal.Target)) return;

                // Получаем позицию цели
                var targetTransform = SystemAPI.GetComponent<LocalToWorld>(goal.Target);
                
                // Переменные для расчета конечной позиции и расстояния остановки
                float3 finalTargetPosition;
                float newStoppingDistance;

                // Обработка разных типов целей
                switch (goal.Type)
                {
                    case GoalType.Harvest:
                        // Настройки сбора ресурсов
                        var harvesterSettings = SystemAPI.GetComponent<HarvesterSettings>(entity);
                        newStoppingDistance = harvesterSettings.InteractionRange * settings.HarvestInteractionRangeBuffer;
                        
                        // Пытаемся найти ближайшую точку на навмеше
                        if (NavMesh.SamplePosition(targetTransform.Position, out NavMeshHit hit, 
                           newStoppingDistance * 2f, NavMesh.AllAreas))
                        {
                            finalTargetPosition = hit.position;
                        }
                        else
                        {
                            // Используем позицию цели как резервный вариант
                            finalTargetPosition = targetTransform.Position;
                        }
                        break;
                        
                    case GoalType.ReturnToBase:
                        // Настройки возврата к базе
                        newStoppingDistance = baseStats.StoppingDistance * settings.ReturnToBaseStoppingDistanceBuffer;
                        
                        // Проверяем наличие смещения для точки прибытия
                        if (SystemAPI.HasComponent<ArrivalPointOffset>(goal.Target))
                        {
                            var offsetComponent = SystemAPI.GetComponent<ArrivalPointOffset>(goal.Target);
                            // Применяем смещение в мировых координатах
                            float3 worldSpaceOffset = math.mul(targetTransform.Rotation, offsetComponent.Value);
                            finalTargetPosition = targetTransform.Position + worldSpaceOffset;
                        }
                        else
                        {
                            finalTargetPosition = targetTransform.Position;
                        }
                        break;
                        
                    default:
                        // Базовые настройки для других типов целей
                        newStoppingDistance = baseStats.StoppingDistance;
                        finalTargetPosition = targetTransform.Position;
                        break;
                }
                
                // Обновляем параметры движения
                movement.TargetPosition = finalTargetPosition;
                movement.HasTarget = true;
                movement.StoppingDistance = newStoppingDistance;
                
                // Обновляем параметры поиска пути
                pathfinding.NeedsPathUpdate = true;
                pathfinding.CurrentWaypointIndex = 0;
                pathfinding.CurrentGoalTarget = goal.Target;

                // Создаем или обновляем запрос на перемещение
                if (!SystemAPI.HasComponent<MoveToRequest>(entity))
                {
                    ecb.AddComponent(entity, new MoveToRequest 
                    { 
                        TargetEntity = goal.Target, 
                        StoppingDistance = newStoppingDistance 
                    });
                }
                else
                {
                    var moveToRequest = SystemAPI.GetComponentRW<MoveToRequest>(entity);
                    moveToRequest.ValueRW.TargetEntity = goal.Target;
                    moveToRequest.ValueRW.StoppingDistance = newStoppingDistance;
                }

            }).Run();
    }
}