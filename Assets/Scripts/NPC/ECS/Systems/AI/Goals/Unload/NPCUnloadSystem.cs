using Unity.Entities;

/// <summary>
/// Система разгрузки инвентаря NPC при возврате на базу.
/// Создает запрос на передачу предметов из инвентаря NPC в поселение.
/// Обновляется в группе SimulationSystemGroup после ReturnToBaseGoalExecutionSystem.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ReturnToBaseGoalExecutionSystem))]
public partial class NPCUnloadSystem : SystemBase
{
    /// <summary>
    /// Основной метод системы, обрабатывающий запросы на разгрузку инвентаря.
    /// Создает запрос на передачу предметов и очищает связанные метки.
    /// </summary>
    protected override void OnUpdate()
    {
        // Получаем командный буфер для изменения сущностей
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        
        // Обрабатываем NPC с запросом на разгрузку и активной целью
        Entities
            .WithAll<UnloadRequestTag, ActiveGoal>()
            .ForEach((Entity entity, in ActiveGoal goal) =>
            {
                // Проверяем, что это цель на возврат на базу
                if (goal.Type != GoalType.ReturnToBase) return;
                
                // Создаем запрос на передачу предметов
                var requestEntity = ecb.CreateEntity();
                ecb.AddComponent(requestEntity, new TransferItemsRequest
                {
                    // Источник - текущий NPC
                    SourceOwner = entity,
                    // Цель - поселение из активной цели
                    DestinationOwner = goal.Target
                });
                
                // Очищаем метки после создания запроса
                ecb.RemoveComponent<UnloadRequestTag>(entity);
                ecb.RemoveComponent<ActiveGoal>(entity);
            }).Schedule();
    }
}