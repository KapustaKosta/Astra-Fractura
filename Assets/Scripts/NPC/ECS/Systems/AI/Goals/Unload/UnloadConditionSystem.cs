using Unity.Entities;
using UnityEngine; 

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

        // 1. Обрабатываем НЕУДАЧНЫЕ попытки разгрузки
        Entities
            .WithAll<TransferFailedTag>()
            .WithoutBurst() 
            .ForEach((Entity entity) =>
            {
                // Блокируем дальнейшие попытки разгрузки из-за ошибки
                ecb.AddComponent<UnloadingBlockedTag>(entity);
                // Очищаем метку неудачи
                ecb.RemoveComponent<TransferFailedTag>(entity);
            }).Schedule();
            
        // 2. Обрабатываем успешные попытки разгрузки
        Entities
            .WithAll<TransferSuccessTag>()
            .WithoutBurst() 
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
                    var oldGoal = SystemAPI.GetComponent<ActiveGoal>(entity);
                    ecb.AddComponent(entity, new CleanupGoalRequest { OldGoalType = oldGoal.Type });
                    ecb.RemoveComponent<ActiveGoal>(entity);
                }
                
                // Очищаем метку успешной передачи
                ecb.RemoveComponent<TransferSuccessTag>(entity);
            }).Schedule();

        // 3. Проверяем, не освободилось ли место на складе
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