using Unity.Entities;

/// <summary>
/// Система-сенсор, которая проверяет, заполнен ли инвентарь карьера,
/// и добавляет или убирает тег `QuarryInventoryFullTag` в зависимости от состояния.
/// Это позволяет другим системам быстро проверять статус инвентаря, не анализируя его содержимое.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(InventorySystem))] // Работает после систем, изменяющих инвентарь
public partial class QuarryInventoryStateSystem : SystemBase
{
    /// <summary>
    /// При создании системы устанавливает требование наличия хотя бы одного карьера для обновления.
    /// </summary>
    protected override void OnCreate()
    {
        RequireForUpdate<QuarryTag>();
    }

    /// <summary>
    /// Выполняется каждый кадр. Проходит по всем карьерам, проверяет, заполнен ли их инвентарь,
    /// и синхронизирует наличие тега `QuarryInventoryFullTag` с реальным состоянием.
    /// </summary>
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        var itemRegistry = ItemRegistry.Instance;
        if (itemRegistry == null) return;

        foreach (var (inventory, entity) in SystemAPI.Query<DynamicBuffer<InventoryItemElement>>().WithAll<QuarryTag>().WithEntityAccess())
        {
            bool isCurrentlyFull = InventoryUtils.IsInventoryFull(inventory, itemRegistry);
            bool hasTag = SystemAPI.HasComponent<QuarryInventoryFullTag>(entity);

            if (isCurrentlyFull && !hasTag)
            {
                ecb.AddComponent<QuarryInventoryFullTag>(entity);
            }
            else if (!isCurrentlyFull && hasTag)
            {
                ecb.RemoveComponent<QuarryInventoryFullTag>(entity);
            }
        }
    }
}