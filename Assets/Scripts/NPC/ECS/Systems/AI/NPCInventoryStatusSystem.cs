using Unity.Entities;

/// <summary>
/// Система отслеживания состояния инвентаря NPC.
/// Управляет метками NPCInventoryEmptyTag и NPCInventoryFullTag в зависимости от заполненности инвентаря.
/// </summary>
public partial class NPCInventoryStatusSystem : SystemBase
{
    /// <summary>
    /// Основной метод системы, обновляющий состояние инвентаря NPC.
    /// Проверяет заполненность инвентаря и управляет соответствующими метками.
    /// </summary>
    protected override void OnUpdate()
    {
        // Получаем доступ к реестру предметов
        var itemRegistry = ItemRegistry.Instance;
        if (itemRegistry == null) return;

        // Создаем командный буфер для изменения сущностей
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

        // Обрабатываем NPC с инвентарем
        Entities
            .WithoutBurst()
            .WithAll<NPCBrain, HasInventoryTag>()
            .ForEach((Entity entity, in DynamicBuffer<InventoryItemElement> inventory) =>
            {
                // Проверяем, пустой ли инвентарь
                bool isEmpty = InventoryUtils.IsInventoryEmpty(inventory);

                // Получаем текущее состояние метки пустого инвентаря
                bool hasEmptyTag = SystemAPI.HasComponent<NPCInventoryEmptyTag>(entity);
                
                // Обновляем метку пустого инвентаря
                if (isEmpty && !hasEmptyTag)
                {
                    ecb.AddComponent<NPCInventoryEmptyTag>(entity);
                }
                else if (!isEmpty && hasEmptyTag)
                {
                    ecb.RemoveComponent<NPCInventoryEmptyTag>(entity);
                }

                // Проверяем, полный ли инвентарь
                bool isFull = InventoryUtils.IsInventoryFull(inventory, itemRegistry);
                
                // Получаем текущее состояние метки полного инвентаря
                bool hasFullTag = SystemAPI.HasComponent<NPCInventoryFullTag>(entity);
                
                // Обновляем метку полного инвентаря
                if (isFull && !hasFullTag)
                {
                    ecb.AddComponent<NPCInventoryFullTag>(entity);
                }
                else if (!isFull && hasFullTag)
                {
                    ecb.RemoveComponent<NPCInventoryFullTag>(entity);
                }

            }).Run();
    }
}