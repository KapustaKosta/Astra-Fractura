using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Синхронизирует компонент ActiveEquippedItem с предметом в активном слоте квикбара.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(QuickbarSelectionSystem))]
public partial class ActiveItemSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

        // Используем WithChangeFilter для оптимизации: система сработает, только если ActiveQuickbarSlot изменился.
        foreach (var (inventory, activeSlot, entity) in SystemAPI.Query<DynamicBuffer<InventoryItemElement>, RefRO<ActiveQuickbarSlot>>()
            .WithAll<PlayerTag>()
            .WithChangeFilter<ActiveQuickbarSlot>()
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

            // Если в выбранном слоте есть предмет
            if (itemInSlot.ItemID != 0)
            {
                var equippedItem = new ActiveEquippedItem { ItemID = itemInSlot.ItemID };

                // Если предмет уже экипирован - обновляем, иначе - добавляем компонент
                if (SystemAPI.HasComponent<ActiveEquippedItem>(entity))
                {
                    ecb.SetComponent(entity, equippedItem);
                }
                else
                {
                    ecb.AddComponent(entity, equippedItem);
                }
            }
            // Если в выбранном слоте пусто
            else
            {
                // А компонент экипировки есть - удаляем его
                if (SystemAPI.HasComponent<ActiveEquippedItem>(entity))
                {
                    ecb.RemoveComponent<ActiveEquippedItem>(entity);
                }
            }
        }
    }
}