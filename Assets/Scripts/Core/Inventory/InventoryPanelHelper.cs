using UnityEngine;
using Unity.Entities;
using System.Collections.Generic;
using System;
using Game.Production;
using Game.Workshop;

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
        InventoryType inventoryType = InventoryType.General,
        Action<InventorySlot> onSlotClickedCallback = null)
    {
        // 1. Очистка старых слотов
        foreach (Transform child in slotsParent)
            UnityEngine.Object.Destroy(child.gameObject);
        slotList.Clear();

        if (!entityManager.Exists(ownerEntity))
            return;

        // 2. Вместимость — из WorkshopConfig для Input/Output/WIP, иначе из InventoryProperties
        int capacity = 0;
        switch (inventoryType)
        {
            case InventoryType.Input:
                if (entityManager.HasComponent<WorkshopConfig>(ownerEntity))
                    capacity = entityManager.GetComponentData<WorkshopConfig>(ownerEntity).InputCapacity;
                break;

            case InventoryType.Output:
                if (entityManager.HasComponent<WorkshopConfig>(ownerEntity))
                    capacity = entityManager.GetComponentData<WorkshopConfig>(ownerEntity).OutputCapacity;
                break;

            case InventoryType.WIP:
                if (entityManager.HasComponent<WorkshopConfig>(ownerEntity))
                    capacity = entityManager.GetComponentData<WorkshopConfig>(ownerEntity).WipCapacity;
                break;

            case InventoryType.General:
            default:
                if (entityManager.HasComponent<InventoryProperties>(ownerEntity))
                    capacity = entityManager.GetComponentData<InventoryProperties>(ownerEntity).Capacity;
                break;
        }

        if (capacity <= 0)
            return;

        // 3. Создание слотов
        for (int i = 0; i < capacity; i++)
        {
            GameObject slotGO = UnityEngine.Object.Instantiate(slotPrefab, slotsParent);
            var slot = slotGO.GetComponent<InventorySlot>();
            if (slot != null)
            {
                // Подписываемся на событие, если колбэк был передан
                if (onSlotClickedCallback != null)
                    slot.OnSlotClicked += onSlotClickedCallback;
                slotList.Add(slot);
            }
        }
    }

    /// <summary>
    /// Обновляет данные в уже существующем списке UI-слотов.
    /// Выбирает правильный тип буфера без Reinterpret.
    /// </summary>
    /// <param name="entityManager">Менеджер сущностей для доступа к данным ECS.</param>
    /// <param name="ownerEntity">Сущность-владелец инвентаря.</param>
    /// <param name="slotList">Список существующих UI-слотов для обновления.</param>
    public static void RefreshSlotsData(
        EntityManager em,
        Entity ownerEntity,
        List<InventorySlot> slotList,
        InventoryType inventoryType = InventoryType.General)
    {
        if (!em.Exists(ownerEntity))
        {
            ClearAllSlots(ownerEntity, slotList, inventoryType);
            return;
        }

        // Резервная очистка если нет ItemRegistry
        var itemRegistry = ItemRegistry.Instance;
        if (itemRegistry == null)
        {
            // Если реестр недоступен, всё равно покажем пустоту, чтобы не подвисать.
            ClearAllSlots(ownerEntity, slotList, inventoryType);
            return;
        }

        switch (inventoryType)
        {
            case InventoryType.Input:
                if (em.HasBuffer<InputInventorySlot>(ownerEntity))
                {
                    var buf = em.GetBuffer<InputInventorySlot>(ownerEntity);
                    RefreshFromTypedBuffer(em, ownerEntity, slotList, buf, inventoryType);
                }
                else ClearAllSlots(ownerEntity, slotList, inventoryType);
                break;

            case InventoryType.Output:
                if (em.HasBuffer<OutputInventorySlot>(ownerEntity))
                {
                    var buf = em.GetBuffer<OutputInventorySlot>(ownerEntity);
                    RefreshFromTypedBuffer(em, ownerEntity, slotList, buf, inventoryType);
                }
                else ClearAllSlots(ownerEntity, slotList, inventoryType);
                break;

            case InventoryType.WIP:
                if (em.HasBuffer<WorkshopWIPBufferElement>(ownerEntity))
                {
                    var buf = em.GetBuffer<WorkshopWIPBufferElement>(ownerEntity);
                    RefreshFromTypedBuffer(em, ownerEntity, slotList, buf, inventoryType);
                }
                else ClearAllSlots(ownerEntity, slotList, inventoryType);
                break;

            case InventoryType.General:
            default:
                if (em.HasBuffer<InventoryItemElement>(ownerEntity))
                {
                    var buf = em.GetBuffer<InventoryItemElement>(ownerEntity);
                    RefreshFromTypedBuffer(em, ownerEntity, slotList, buf, inventoryType);
                }
                else ClearAllSlots(ownerEntity, slotList, inventoryType);
                break;
        }
    }

    /// <summary>
    /// Типобезопасное чтение универсального буфера InventoryItemElement.
    /// </summary>
    private static void RefreshFromTypedBuffer(
        EntityManager em,
        Entity ownerEntity,
        List<InventorySlot> slotList,
        DynamicBuffer<InventoryItemElement> buffer,
        InventoryType inventoryType)
    {
#if UNITY_EDITOR
        if (buffer.Length > 0)
            Debug.Log($"[InventoryUI] {ownerEntity} General buf.len={buffer.Length} first=({buffer[0].ItemID},{buffer[0].Amount})");
#endif

        var itemRegistry = ItemRegistry.Instance;
        for (int i = 0; i < slotList.Count; i++)
        {
            if (i >= buffer.Length)
            {
                slotList[i].InitializeSlot(null, 0, ownerEntity, i, inventoryType);
                continue;
            }

            var el = buffer[i];
            if (el.ItemID != 0 && el.Amount > 0)
            {
                var data = itemRegistry.GetItemData(el.ItemID);
                slotList[i].InitializeSlot(data, el.Amount, ownerEntity, i, inventoryType);
            }
            else
            {
                slotList[i].InitializeSlot(null, 0, ownerEntity, i, inventoryType);
            }
        }
    }

    /// <summary>
    /// Типобезопасное чтение буфера InputInventorySlot.
    /// </summary>
    private static void RefreshFromTypedBuffer(
        EntityManager em,
        Entity ownerEntity,
        List<InventorySlot> slotList,
        DynamicBuffer<InputInventorySlot> buffer,
        InventoryType inventoryType)
    {
#if UNITY_EDITOR
        if (buffer.Length > 0)
            Debug.Log($"[InventoryUI] {ownerEntity} INPUT buf.len={buffer.Length} first=({buffer[0].ItemID},{buffer[0].Amount})");
#endif

        var itemRegistry = ItemRegistry.Instance;
        for (int i = 0; i < slotList.Count; i++)
        {
            if (i >= buffer.Length)
            {
                slotList[i].InitializeSlot(null, 0, ownerEntity, i, inventoryType);
                continue;
            }

            var el = buffer[i];
            if (el.ItemID != 0 && el.Amount > 0)
            {
                var data = itemRegistry.GetItemData(el.ItemID);
                slotList[i].InitializeSlot(data, el.Amount, ownerEntity, i, inventoryType);
            }
            else
            {
                slotList[i].InitializeSlot(null, 0, ownerEntity, i, inventoryType);
            }
        }
    }

    /// <summary>
    /// Типобезопасное чтение буфера OutputInventorySlot.
    /// </summary>
    private static void RefreshFromTypedBuffer(
        EntityManager em,
        Entity ownerEntity,
        List<InventorySlot> slotList,
        DynamicBuffer<OutputInventorySlot> buffer,
        InventoryType inventoryType)
    {
#if UNITY_EDITOR
        if (buffer.Length > 0)
            Debug.Log($"[InventoryUI] {ownerEntity} OUTPUT buf.len={buffer.Length} first=({buffer[0].ItemID},{buffer[0].Amount})");
#endif

        var itemRegistry = ItemRegistry.Instance;
        for (int i = 0; i < slotList.Count; i++)
        {
            if (i >= buffer.Length)
            {
                slotList[i].InitializeSlot(null, 0, ownerEntity, i, inventoryType);
                continue;
            }

            var el = buffer[i];
            if (el.ItemID != 0 && el.Amount > 0)
            {
                var data = itemRegistry.GetItemData(el.ItemID);
                slotList[i].InitializeSlot(data, el.Amount, ownerEntity, i, inventoryType);
            }
            else
            {
                slotList[i].InitializeSlot(null, 0, ownerEntity, i, inventoryType);
            }
        }
    }

    /// <summary>
    /// Типобезопасное чтение буфера WIP (буферный инвентарь цеха).
    /// </summary>
    private static void RefreshFromTypedBuffer(
        EntityManager em,
        Entity ownerEntity,
        List<InventorySlot> slotList,
        DynamicBuffer<WorkshopWIPBufferElement> buffer,
        InventoryType inventoryType)
    {
#if UNITY_EDITOR
        if (buffer.Length > 0)
            Debug.Log($"[InventoryUI] {ownerEntity} WIP buf.len={buffer.Length} first=({buffer[0].ItemID},{buffer[0].Amount})");
#endif

        var itemRegistry = ItemRegistry.Instance;
        for (int i = 0; i < slotList.Count; i++)
        {
            if (i >= buffer.Length)
            {
                slotList[i].InitializeSlot(null, 0, ownerEntity, i, inventoryType);
                continue;
            }

            var el = buffer[i];
            if (el.ItemID != 0 && el.Amount > 0)
            {
                var data = itemRegistry.GetItemData(el.ItemID);
                slotList[i].InitializeSlot(data, el.Amount, ownerEntity, i, inventoryType);
            }
            else
            {
                slotList[i].InitializeSlot(null, 0, ownerEntity, i, inventoryType);
            }
        }
    }

    /// <summary>
    /// Очистка слотов (когда буфера нет/пуст).
    /// </summary>
    private static void ClearAllSlots(Entity ownerEntity, List<InventorySlot> slotList, InventoryType inventoryType)
    {
        for (int i = 0; i < slotList.Count; i++)
            slotList[i].InitializeSlot(null, 0, ownerEntity, i, inventoryType);
    }
}