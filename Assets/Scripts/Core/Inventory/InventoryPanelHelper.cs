using UnityEngine;
using Unity.Entities;
using System.Collections.Generic;
using System; // Для Action

/// <summary>
/// Статический класс-помощник, содержащий общую логику для создания и обновления
/// UI-панелей инвентаря. Используется в InventoryUI и TradeUI.
/// </summary>
public static class InventoryPanelHelper
{
    /// <summary>
    /// Полностью перестраивает UI-слоты для указанного инвентаря.
    /// Уничтожает старые слоты, создает новые и добавляет их в предоставленный список.
    /// </summary>
    /// <param name="entityManager">Менеджер сущностей для доступа к данным ECS.</param>
    /// <param name="ownerEntity">Сущность-владелец инвентаря.</param>
    /// <param name="slotsParent">Трансформ-контейнер для UI-слотов.</param>
    /// <param name="slotPrefab">Префаб одного UI-слота.</param>
    /// <param name="slotList">Список, в который будут добавлены созданные слоты.</param>
    /// <param name="onSlotClickedCallback">Необязательный колбэк, который будет подписан на событие клика по слоту.</param>
    public static void RebuildSlots(
        EntityManager entityManager,
        Entity ownerEntity,
        Transform slotsParent,
        GameObject slotPrefab,
        List<InventorySlot> slotList,
        Action<Item> onSlotClickedCallback = null)
    {
        // 1. Очистка старых слотов
        foreach (Transform child in slotsParent)
        {
            UnityEngine.Object.Destroy(child.gameObject);
        }
        slotList.Clear();

        // 2. Проверка, есть ли у сущности инвентарь
        if (!entityManager.Exists(ownerEntity) || !entityManager.HasComponent<InventoryProperties>(ownerEntity))
        {
            return;
        }

        var properties = entityManager.GetComponentData<InventoryProperties>(ownerEntity);

        // 3. Создание новых слотов
        for (int i = 0; i < properties.Capacity; i++)
        {
            GameObject slotGO = UnityEngine.Object.Instantiate(slotPrefab, slotsParent);
            InventorySlot slot = slotGO.GetComponent<InventorySlot>();
            if (slot != null)
            {
                // Подписываемся на событие, если колбэк был передан
                if (onSlotClickedCallback != null)
                {
                    slot.OnSlotClicked += onSlotClickedCallback;
                }
                slotList.Add(slot);
            }
        }
    }

    /// <summary>
    /// Обновляет данные в уже существующем списке UI-слотов на основе текущего состояния инвентаря в ECS.
    /// </summary>
    /// <param name="entityManager">Менеджер сущностей для доступа к данным ECS.</param>
    /// <param name="ownerEntity">Сущность-владелец инвентаря.</param>
    /// <param name="slotList">Список существующих UI-слотов для обновления.</param>
    public static void RefreshSlotsData(
        EntityManager entityManager,
        Entity ownerEntity,
        List<InventorySlot> slotList)
    {
        // 1. Проверка наличия инвентаря
        if (!entityManager.Exists(ownerEntity) || !entityManager.HasBuffer<InventoryItemElement>(ownerEntity))
        {
            // Если инвентаря нет, просто очищаем все слоты, но сохраняем их контекст
            for (int i = 0; i < slotList.Count; i++)
            {
                slotList[i].InitializeSlot(null, 0, ownerEntity, i);
            }
            return;
        }

        var inventoryBuffer = entityManager.GetBuffer<InventoryItemElement>(ownerEntity);
        var itemRegistry = ItemRegistry.Instance; 

        if (itemRegistry == null) return;

        // 2. Итерация по UI-слотам и синхронизация их с данными из буфера
        for (int i = 0; i < slotList.Count; i++)
        {
            // Проверяем, что в буфере есть соответствующий элемент
            if (i >= inventoryBuffer.Length)
            {
                slotList[i].InitializeSlot(null, 0, ownerEntity, i);
                continue;
            }

            var itemElement = inventoryBuffer[i];
            
            // Если ItemID не равен 0, значит, в слоте есть предмет
            if (itemElement.ItemID != 0)
            {
                var itemData = itemRegistry.GetItemData(itemElement.ItemID);
                slotList[i].InitializeSlot(itemData, itemElement.Amount, ownerEntity, i);
            }
            else
            {
                // Иначе это пустой слот
                slotList[i].InitializeSlot(null, 0, ownerEntity, i);
            }
        }
    }
}