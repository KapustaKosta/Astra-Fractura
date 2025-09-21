using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Система выполнения цели "Сбор ресурсов" для ИИ-агентов.
/// Управляет переходом NPC в режим добычи при достижении цели,
/// контролируя начало и завершение процесса сбора ресурсов.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(NPCTaskCleanupSystem))]
public partial class HarvestGoalExecutionSystem : SystemBase
{
    /// <summary>
    /// Основной метод системы, обрабатывающий выполнение цели сбора ресурсов.
    /// Управляет перемещением NPC и установкой меток для начала сбора.
    /// </summary>
    protected override void OnUpdate()
    {
        var entityManager = this.EntityManager;

        Entities
            .WithStructuralChanges()
            .ForEach((Entity entity, 
                     ref NPCMovementComponent movement, 
                     in ActiveGoal goal, 
                     in HarvesterSettings harvesterSettings, 
                     in LocalToWorld npcTransform,
                     in DynamicBuffer<NPCPathBufferElement> pathBuffer) => 
            {
                // Проверяем базовые условия
                if (goal.Type != GoalType.Harvest || 
                    goal.Target == Entity.Null || 
                    !entityManager.HasComponent<LocalToWorld>(goal.Target) ||
                    pathBuffer.Length == 0) 
                {
                    return;
                }
                
                // 1. NPC считает, что он прибыл (движение остановлено)
                bool hasArrived = !movement.HasTarget;
                
                // 2. Получаем конечную точку, к которой он реально шел
                float3 finalWaypoint = pathBuffer[pathBuffer.Length - 1].Waypoint;

                // 3. Проверяем, действительно ли NPC находится у конечной точки пути
                //    Используем квадрат радиуса остановки для точности
                float stopRadius = movement.StoppingDistance + 0.2f; // небольшой запас
                bool isAtFinalWaypoint = math.distancesq(npcTransform.Position, finalWaypoint) 
                                         <= stopRadius * stopRadius;

                // 4. Дополнительно проверяем, что сама конечная точка пути находится в рендже взаимодействия с целью.
                //    Это защита от случаев, когда путь построен к точке, которая на самом деле далеко от ресурса.
                var targetTransform = entityManager.GetComponentData<LocalToWorld>(goal.Target);
                bool isWaypointCloseToTarget = math.distancesq(finalWaypoint, targetTransform.Position) 
                                               <= harvesterSettings.InteractionRange * harvesterSettings.InteractionRange;

                // Проверяем наличие метки активного сбора
                bool wantsToHarvest = entityManager.HasComponent<WantsToHarvestTag>(entity);
                
                if (hasArrived && isAtFinalWaypoint && isWaypointCloseToTarget)
                {
                    // Условия выполнены - начинаем или продолжаем сбор
                    if (!wantsToHarvest)
                    {
                        // Активируем режим сбора:
                        entityManager.AddComponent<WantsToHarvestTag>(entity);
                        // Устанавливаем целевой объект для взаимодействия
                        entityManager.AddComponentData(entity, new ActiveTarget { Value = goal.Target });
                    }
                }
                else
                {
                    // Условия НЕ выполнены - прекращаем сбор
                    if (wantsToHarvest)
                    {
                        // Деактивируем режим сбора:
                        entityManager.RemoveComponent<WantsToHarvestTag>(entity);
                        // Очищаем ссылку на активную цель
                        entityManager.RemoveComponent<ActiveTarget>(entity);
                    }
                }

            }).Run();
    }
}