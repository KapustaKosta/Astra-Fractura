using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Синхронизирует компонент ActiveEquippedItem с предметом в активном слоте квикбара.
/// <para>
/// Эта система является "хранителем правды" для экипированного предмета. Она срабатывает
/// только при необходимости (смена слота или изменение инвентаря), обеспечивая высокую
/// производительность и гарантируя, что ActiveEquippedItem всегда актуален.
/// </para>
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(QuickbarSelectionSystem))]
[UpdateAfter(typeof(InventorySystem))]
public partial class ActiveItemSystem : SystemBase
{
    private EntityQuery m_slotChangedQuery;
    private EntityQuery m_inventoryChangedQuery;

    /// <summary>
    /// Вызывается при создании системы для инициализации запросов,
    /// которые будут служить триггерами для обновления.
    /// </summary>
    protected override void OnCreate()
    {
        // Создаем запрос, который будет содержать игрока, только если его компонент ActiveQuickbarSlot изменился.
        // Это наш первый триггер.
        m_slotChangedQuery = GetEntityQuery(ComponentType.ReadOnly<PlayerTag>(), ComponentType.ReadOnly<ActiveQuickbarSlot>());
        m_slotChangedQuery.AddChangedVersionFilter(typeof(ActiveQuickbarSlot));
            
        // Создаем запрос, который будет содержать игрока, только если у него есть тег InventoryChangedTag.
        // Это наш второй триггер.
        m_inventoryChangedQuery = GetEntityQuery(ComponentType.ReadOnly<PlayerTag>(), ComponentType.ReadOnly<InventoryChangedTag>());
    }

    /// <summary>
    /// Вызывается для синхронизации экипированного предмета при необходимости.
    /// </summary>
    protected override void OnUpdate()
    {
        // Ранний выход: если ни один из наших триггеров не сработал (ни слот не менялся,
        // ни инвентарь не обновлялся), система немедленно прекращает работу. Это главная оптимизация.
        if (m_slotChangedQuery.IsEmpty && m_inventoryChangedQuery.IsEmpty)
        {
            return;
        }

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

        // Основная логика выполняется для сущности игрока.
        // Мы уже знаем, что она должна сработать, поэтому здесь не нужны сложные фильтры.
        foreach (var (inventory, activeSlot, entity) in SystemAPI.Query<DynamicBuffer<InventoryItemElement>, RefRO<ActiveQuickbarSlot>>()
            .WithAll<PlayerTag>()
            .WithEntityAccess())
        {
            // Проверяем, что инвентарь и индекс валидны
            if (activeSlot.ValueRO.Index >= inventory.Length)
            {
                // Если активного слота нет, а предмет "экипирован" - снимаем его
                if (SystemAPI.HasComponent<ActiveEquippedItem>(entity))
                {
                    ecb.RemoveComponent<ActiveEquippedItem>(entity);
                }
                continue;
            }
            
            var itemInSlot = inventory[activeSlot.ValueRO.Index];
            bool hasEquippedItemComponent = SystemAPI.HasComponent<ActiveEquippedItem>(entity);

            // Случай 1: В активном слоте есть предмет.
            if (itemInSlot.ItemID != 0)
            {
                var newEquippedItem = new ActiveEquippedItem { ItemID = itemInSlot.ItemID };

                // Если предмет уже был экипирован, обновляем компонент, только если ID изменился.
                if (hasEquippedItemComponent)
                {
                    var currentEquippedItem = SystemAPI.GetComponent<ActiveEquippedItem>(entity);
                    if (currentEquippedItem.ItemID != newEquippedItem.ItemID)
                    {
                        ecb.SetComponent(entity, newEquippedItem);
                    }
                }
                // Если предмета в руках не было, добавляем компонент.
                else
                {
                    ecb.AddComponent(entity, newEquippedItem);
                }
            }
            // Если в выбранном слоте пусто
            else
            {
                // Если предмет был экипирован, удаляем компонент.
                if (hasEquippedItemComponent)
                {
                    ecb.RemoveComponent<ActiveEquippedItem>(entity);
                }
            }
        }
    }
}