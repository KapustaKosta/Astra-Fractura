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
        // Получаем прямой доступ к EntityManager для немедленного изменения компонентов
        var entityManager = this.EntityManager;

        // Обрабатываем всех сущностей с ИИ-движением и активной целью
        Entities
            .WithStructuralChanges() // Разрешаем структурные изменения в текущем потоке
            .ForEach((Entity entity, ref NPCMovementComponent movement, in ActiveGoal goal, 
                     in HarvesterSettings harvesterSettings, in LocalToWorld npcTransform) =>
            {
                // Проверяем, что цель - сбор ресурсов и есть корректная целевая сущность
                if (goal.Type != GoalType.Harvest || 
                    goal.Target == Entity.Null || 
                    !entityManager.HasComponent<LocalToWorld>(goal.Target)) 
                {
                    return;
                }

                // Проверяем, достиг ли NPC цели (движение завершено)
                bool hasArrived = !movement.HasTarget;
                
                // Получаем позицию цели
                var targetTransform = entityManager.GetComponentData<LocalToWorld>(goal.Target);
                
                // Проверяем, находится ли NPC в радиусе взаимодействия
                // Используем distance squared для избежания вычисления корня
                bool isInRange = math.distancesq(npcTransform.Position, targetTransform.Position) 
                               <= harvesterSettings.InteractionRange * harvesterSettings.InteractionRange;

                // Проверяем наличие метки активного сбора
                bool wantsToHarvest = entityManager.HasComponent<WantsToHarvestTag>(entity);

                // Логика управления состоянием сбора:
                if (hasArrived && isInRange)
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