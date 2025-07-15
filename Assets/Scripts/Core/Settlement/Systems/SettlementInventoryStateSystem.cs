using Unity.Entities;

/// <summary>
/// Единая система-сенсор, которая проверяет состояние инвентаря поселения.
/// Если инвентарь полностью заполнен (нет пустых слотов и все стаки максимальны), 
/// она добавляет тег SettlementInventoryFullTag.
/// Это позволяет другим системам быстро принимать решения, не проверяя инвентарь каждый раз.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
// Запускаемся после изменения инвентарей, но до того, как Арбитр примет решение
[UpdateAfter(typeof(InventorySystem))]
[UpdateBefore(typeof(NPCTaskArbiterSystem))]
public partial class SettlementInventoryStateSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<PlayerSettlementTag>();
    }

    protected override void OnUpdate()
    {
        // Получаем доступ к реестру предметов.
        var itemRegistry = ItemRegistry.Instance;
        if (itemRegistry == null) return; // Реестр еще не инициализирован, выходим.

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        var settlementEntity = SystemAPI.GetSingletonEntity<PlayerSettlementTag>();
        
        if (!SystemAPI.HasBuffer<InventoryItemElement>(settlementEntity))
        {
            if (SystemAPI.HasComponent<SettlementInventoryFullTag>(settlementEntity))
            {
                ecb.RemoveComponent<SettlementInventoryFullTag>(settlementEntity);
            }
            return;
        }

        // Получаем буфер в режиме только для чтения, так как мы его не меняем.
        var inventory = SystemAPI.GetBuffer<InventoryItemElement>(settlementEntity);
        
        // Используем унифицированную функцию из утилиты InventoryUtils для проверки, полон ли инвентарь.
        // Эта проверка вернет true, только если нет ни пустых слотов, ни неполных стаков.
        bool isCurrentlyFull = InventoryUtils.IsInventoryFull(inventory, itemRegistry);

        bool hasTag = SystemAPI.HasComponent<SettlementInventoryFullTag>(settlementEntity);

        // Обновляем состояние только если оно изменилось
        if (isCurrentlyFull && !hasTag)
        {
            // Инвентарь заполнился - добавляем тег
            ecb.AddComponent<SettlementInventoryFullTag>(settlementEntity);
        }
        else if (!isCurrentlyFull && hasTag)
        {
            // В инвентаре освободилось место - убираем тег
            ecb.RemoveComponent<SettlementInventoryFullTag>(settlementEntity);
        }
    }
}