using Unity.Entities;

/// <summary>
/// Система условий разгрузки инвентаря NPC.
/// Обрабатывает результаты передачи предметов и управляет блокировками.
/// Обновляется в группе SimulationSystemGroup перед NPCTaskArbiterSystem.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(NPCTaskArbiterSystem))]
public partial class UnloadConditionSystem : SystemBase
{
    /// <summary>
    /// Основной метод системы, обрабатывающий результаты передачи предметов.
    /// Управляет блокировками разгрузки и очищает метки после завершения операций.
    /// </summary>
    protected override void OnUpdate()
    {
        // Получаем командный буфер для изменения сущностей
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);

        // Обрабатываем неудачные попытки разгрузки
        Entities
            .WithAll<TransferFailedTag>()
            .WithoutBurst() // Требуется для немедленного выполнения через .Run()
            .ForEach((Entity entity) =>
            {
                // Блокируем дальнейшие попытки разгрузки из-за ошибки
                ecb.AddComponent<UnloadingBlockedTag>(entity);
                // Очищаем метку неудачи
                ecb.RemoveComponent<TransferFailedTag>(entity);
            }).Run(); 
            
        // Обрабатываем успешные попытки разгрузки
        Entities
            .WithAll<TransferSuccessTag>()
            .WithoutBurst() // Требуется для немедленного выполнения через .Run()
            .ForEach((Entity entity) =>
            {
                // Снимаем блокировку, если она была
                if(SystemAPI.HasComponent<UnloadingBlockedTag>(entity))
                {
                    ecb.RemoveComponent<UnloadingBlockedTag>(entity);
                }
                
                // Очищаем активную цель после успешной разгрузки
                if(SystemAPI.HasComponent<ActiveGoal>(entity))
                {
                    ecb.RemoveComponent<ActiveGoal>(entity);
                }
                
                // Очищаем метку успешной передачи
                ecb.RemoveComponent<TransferSuccessTag>(entity);
            }).Run();

        // Проверяем наличие свободного места на складе
        if (SystemAPI.TryGetSingletonEntity<PlayerSettlementTag>(out Entity settlementEntity))
        {
            // Если склад не переполнен
            if (!SystemAPI.HasComponent<SettlementInventoryFullTag>(settlementEntity))
            {
                // Снимаем блокировку со всех NPC, которые не могли разгрузиться
                Entities
                    .WithAll<UnloadingBlockedTag>()
                    .ForEach((Entity entity) =>
                    {
                        ecb.RemoveComponent<UnloadingBlockedTag>(entity);
                    }).Schedule();
            }
        }
    }
}