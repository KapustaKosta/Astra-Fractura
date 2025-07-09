using UnityEngine;
using Unity.Entities;
using System.Collections.Generic;

/// <summary>
/// Authoring-компонент для определения начального инвентаря сущности.
/// Позволяет задать вместимость инвентаря и список стартовых предметов через инспектор Unity.
/// </summary>
public class StartingInventoryAuthoring : MonoBehaviour
{
    /// <summary>
    /// Внутренний класс для удобного задания стартовых предметов в инспекторе.
    /// </summary>
    [System.Serializable]
    public class StartingItem
    {
        /// <summary>
        /// ScriptableObject предмета.
        /// </summary>
        public Item item;
        /// <summary>
        /// Количество этого предмета в стартовом наборе.
        /// </summary>
        [Range(1, 9999)]
        public int amount = 1;
    }

    [Header("Inventory Properties")]
    [Tooltip("Общая вместимость инвентаря в слотах.")]
    public int capacity = 20;

    [Header("Starting Content")]
    [Tooltip("Список предметов, которые будут находиться в инвентаре при создании сущности.")]
    public List<StartingItem> startingItems;

    /// <summary>
    /// Baker-класс для преобразования данных из MonoBehaviour в ECS-компоненты.
    /// </summary>
    private class Baker : Baker<StartingInventoryAuthoring>
    {
        /// <summary>
        /// Выполняет процесс "запекания". Добавляет к сущности тег HasInventoryTag,
        /// компонент InventoryProperties и буфер InventoryItemElement, заполненный
        /// стартовыми предметами и пустыми слотами.
        /// </summary>
        /// <param name="authoring">Экземпляр StartingInventoryAuthoring.</param>
        public override void Bake(StartingInventoryAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Добавляем необходимые компоненты для функционирования инвентаря.
            AddComponent<HasInventoryTag>(entity);
            AddComponent(entity, new InventoryProperties { Capacity = authoring.capacity });
            
            var inventoryBuffer = AddBuffer<InventoryItemElement>(entity);
            
            // Резервируем место в буфере под полную вместимость инвентаря.
            inventoryBuffer.ResizeUninitialized(authoring.capacity);

            // Заполняем все слоты "пустыми" предметами (с ItemID = 0) по умолчанию.
            for (int i = 0; i < authoring.capacity; i++)
            {
                inventoryBuffer[i] = new InventoryItemElement { ItemID = 0, Amount = 0 };
            }

            // Размещаем стартовые предметы в начальные слоты инвентаря.
            int currentSlot = 0;
            foreach (var startingItem in authoring.startingItems)
            {
                if (currentSlot >= authoring.capacity)
                {
                    Debug.LogWarning($"[StartingInventoryAuthoring] Недостаточно места в инвентаре для всех стартовых предметов на '{authoring.name}'.", authoring.gameObject);
                    break;
                }
                
                if (startingItem == null || startingItem.item == null || startingItem.amount <= 0) continue;
                
                Item item = startingItem.item;
                if (item.itemID == 0)
                {
                    Debug.LogError($"[StartingInventoryAuthoring] У стартового предмета '{item.name}' невалидный ItemID (0).", authoring.gameObject);
                    continue;
                }

                // Записываем данные о стартовом предмете в соответствующий слот буфера.
                inventoryBuffer[currentSlot] = new InventoryItemElement
                {
                    ItemID = item.itemID,
                    Amount = startingItem.amount
                };
                currentSlot++;
            }
        }
    }
}