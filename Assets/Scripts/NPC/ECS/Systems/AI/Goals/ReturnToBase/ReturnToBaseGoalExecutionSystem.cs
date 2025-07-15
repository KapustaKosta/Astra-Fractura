using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Система выполнения цели "Вернуться на базу" для ИИ.
/// Управляет перемещением NPC к поселению и инициирует разгрузку инвентаря.
/// Обновляется в группе SimulationSystemGroup после NPCTaskCleanupSystem.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(NPCTaskCleanupSystem))]
public partial class ReturnToBaseGoalExecutionSystem : SystemBase
{
    /// <summary>
    /// Основной метод системы, обрабатывающий выполнение цели возврата на базу.
    /// Управляет движением NPC к поселению и инициирует разгрузку при достижении цели.
    /// </summary>
    protected override void OnUpdate()
    {
        // Получаем командный буфер для изменения сущностей
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        // Получаем глобальные настройки AI
        var settings = SystemAPI.GetSingleton<AISettings>();
        
        Entities
            .ForEach((Entity entity, in ActiveGoal goal, in NPCMovementComponent movementSettings) =>
            {
                // Проверяем, что это цель на возврат на базу
                if (goal.Type != GoalType.ReturnToBase) return;
                
                // Проверяем, существует ли цель (поселение)
                if (!SystemAPI.HasComponent<LocalToWorld>(goal.Target)) return;
                
                // Получаем позиции NPC и цели
                var npcTransform = SystemAPI.GetComponent<LocalToWorld>(entity);
                var targetTransform = SystemAPI.GetComponent<LocalToWorld>(goal.Target);
                
                // Рассчитываем квадрат расстояния до цели
                float distanceSq = math.distancesq(npcTransform.Position, targetTransform.Position);
                // Проверяем, находится ли NPC в зоне остановки
                bool isInRange = distanceSq <= movementSettings.StoppingDistance * movementSettings.StoppingDistance;

                if (!isInRange)
                {
                    // NPC не в зоне - запрашиваем движение к цели
                    if (!SystemAPI.HasComponent<MoveToRequest>(entity))
                    {
                        // Уменьшаем дистанцию остановки с учётом буфера, но не меньше 0.1
                        float targetStoppingDistance = movementSettings.StoppingDistance - settings.ReturnToBaseStoppingDistanceBuffer; 
                        ecb.AddComponent(entity, new MoveToRequest 
                        { 
                            TargetEntity = goal.Target, 
                            StoppingDistance = math.max(0.1f, targetStoppingDistance) // Минимальная дистанция 0.1
                        });
                    }
                }
                else
                {
                    // NPC в зоне - останавливаем движение
                    if (SystemAPI.HasComponent<MoveToRequest>(entity))
                    {
                        ecb.RemoveComponent<MoveToRequest>(entity);
                    }
                    
                    // Используем новый тег UnloadRequestTag для инициации разгрузки
                    if (!SystemAPI.HasComponent<UnloadRequestTag>(entity))
                    {
                       ecb.AddComponent<UnloadRequestTag>(entity);
                    }
                }
            }).Schedule();
    }
}