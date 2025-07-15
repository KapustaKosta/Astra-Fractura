using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Система выполнения цели "Сбор ресурсов" для ИИ.
/// Обрабатывает логику перемещения NPC к ресурсу и началу сбора.
/// Обновляется в группе SimulationSystemGroup после NPCTaskCleanupSystem.
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
        // Получаем командный буфер и настройки AI
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        var settings = SystemAPI.GetSingleton<AISettings>();

        Entities
            .ForEach((Entity entity, in ActiveGoal goal, in HarvesterSettings harvesterSettings) =>
            {
                // Проверяем, что это цель на сбор ресурсов
                if (goal.Type != GoalType.Harvest) return;
                
                // Проверяем существование цели (если ресурс уничтожен, пропускаем)
                if (!SystemAPI.HasComponent<LocalToWorld>(goal.Target)) return;
                
                // Получаем позиции NPC и цели
                var targetTransform = SystemAPI.GetComponent<LocalToWorld>(goal.Target);
                var npcTransform = SystemAPI.GetComponent<LocalToWorld>(entity);
                
                // Рассчитываем расстояние до цели
                float sqrDistance = math.distancesq(npcTransform.Position, targetTransform.Position);
                float interactionRange = harvesterSettings.InteractionRange * settings.HarvestInteractionRangeBuffer;
                bool isInRange = sqrDistance <= interactionRange * interactionRange;

                if (isInRange)
                {
                    // NPC на месте - останавливаем движение и устанавливаем метки
                    if (SystemAPI.HasComponent<MoveToRequest>(entity))
                        ecb.RemoveComponent<MoveToRequest>(entity);
                    
                    if (!SystemAPI.HasComponent<IsAtHarvestTargetTag>(entity))
                        ecb.AddComponent<IsAtHarvestTargetTag>(entity);
                    
                    // Устанавливаем намерение собирать ресурсы и активную цель
                    if (!SystemAPI.HasComponent<WantsToHarvestTag>(entity))
                    {
                        ecb.AddComponent<WantsToHarvestTag>(entity);
                        ecb.AddComponent(entity, new ActiveTarget { Value = goal.Target }); 
                    }
                }
                else 
                {
                    // NPC далеко от цели - организуем движение
                    if (SystemAPI.HasComponent<IsAtHarvestTargetTag>(entity))
                        ecb.RemoveComponent<IsAtHarvestTargetTag>(entity);
                    
                    // Отменяем намерение собирать и очищаем цель
                    if (SystemAPI.HasComponent<WantsToHarvestTag>(entity))
                    {
                        ecb.RemoveComponent<WantsToHarvestTag>(entity);
                        ecb.RemoveComponent<ActiveTarget>(entity); 
                    }
                    
                    // Запрашиваем движение к цели, если еще не движемся
                    if (!SystemAPI.HasComponent<MoveToRequest>(entity))
                    {
                        ecb.AddComponent(entity, new MoveToRequest {
                            TargetEntity = goal.Target,
                            StoppingDistance = interactionRange 
                        });
                    }
                }
            }).Schedule();
    }
}