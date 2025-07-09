using UnityEngine;
using Unity.Entities;
using System.Collections.Generic;

/// <summary>
/// Authoring-компонент для определения стартового набора предметов для любой сущности.
/// Позволяет удобно настроить начальный инвентарь в инспекторе.
/// </summary>
public class StartingInventoryAuthoring : MonoBehaviour
{
    /// <summary>
    /// Вложенный класс для удобной настройки пар "предмет-количество" в инспекторе.
    /// </summary>
    [System.Serializable]
    public class StartingItem
    {
        [Tooltip("Ассет предмета")]
        public Item item;
        
        [Tooltip("Количество этого предмета")]
        [Range(1, 9999)]
        public int amount = 1;
    }

    [Header("Inventory Properties")]
    [Tooltip("Общая вместимость инвентаря (количество слотов).")]
    public int capacity = 20;

    [Header("Starting Content")]
    [Tooltip("Список стартовых предметов и их количество.")]
    public List<StartingItem> startingItems;

    /// <summary>
    /// Baker-класс для преобразования данных из MonoBehaviour в ECS-компоненты.
    /// </summary>
    private class Baker : Baker<StartingInventoryAuthoring>
    {
        /// <summary>
        /// Выполняет процесс "запекания", добавляя компоненты инвентаря и наполняя его стартовыми предметами.
        /// </summary>
        /// <param name="authoring">Экземпляр StartingInventoryAuthoring.</param>
        public override void Bake(StartingInventoryAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Добавляем необходимые компоненты для функционирования инвентаря.
            AddComponent<HasInventoryTag>(entity);
            AddComponent(entity, new InventoryProperties { Capacity = authoring.capacity });
            
            var inventoryBuffer = AddBuffer<InventoryItemElement>(entity);

            // Итерируемся по списку стартовых предметов и добавляем их в буфер.
            foreach (var startingItem in authoring.startingItems)
            {
                if (startingItem == null || startingItem.item == null || startingItem.amount <= 0)
                {
                    continue;
                }
                
                Item item = startingItem.item;
                int amount = startingItem.amount;

                if (item.itemID == 0)
                {
                    Debug.LogError($"[StartingInventoryAuthoring] У стартового предмета '{item.name}' на объекте '{authoring.name}' невалидный ItemID (0). Предмет не будет добавлен.", authoring.gameObject);
                    continue;
                }
                
                inventoryBuffer.Add(new InventoryItemElement
                {
                    ItemID = item.itemID,
                    Amount = amount
                });
            }
        }
    }
}