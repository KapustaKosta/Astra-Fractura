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
    /// </summary>
    public static void RebuildSlots(
        EntityManager entityManager,
        Entity ownerEntity,
        Transform slotsParent,
        GameObject slotPrefab,
        List<InventorySlot> slotList,
        InventoryType inventoryType = InventoryType.General,
        Action<InventorySlot> onSlotClickedCallback = null)
    {
        foreach (Transform child in slotsParent)
            UnityEngine.Object.Destroy(child.gameObject);
        slotList.Clear();

        if (!entityManager.Exists(ownerEntity))
            return;

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

        if (capacity <= 0) return;

        for (int i = 0; i < capacity; i++)
        {
            GameObject slotGO = UnityEngine.Object.Instantiate(slotPrefab, slotsParent);
            var slot = slotGO.GetComponent<InventorySlot>();
            if (slot != null)
            {
                if (onSlotClickedCallback != null)
                    slot.OnSlotClicked += onSlotClickedCallback;
                slotList.Add(slot);
            }
        }
    }

    /// <summary>
    /// Обновляет данные в уже существующем списке UI-слотов, читая каждый тип буфера напрямую.
    /// </summary>
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

        var itemRegistry = ItemRegistry.Instance;
        if (itemRegistry == null)
        {
            Debug.LogError("[InventoryPanelHelper] ItemRegistry.Instance is null! Cannot refresh slots.");
            ClearAllSlots(ownerEntity, slotList, inventoryType);
            return;
        }

        // Мы больше не используем Reinterpret. Вместо этого мы обрабатываем каждый тип буфера напрямую.
        switch (inventoryType)
        {
            case InventoryType.Input:
                if (em.HasBuffer<InputInventorySlot>(ownerEntity))
                {
                    var buffer = em.GetBuffer<InputInventorySlot>(ownerEntity);
                    for (int i = 0; i < slotList.Count; i++)
                    {
                        if (i < buffer.Length)
                        {
                            var el = buffer[i];
                            var itemData = el.ItemID != 0 ? itemRegistry.GetItemData(el.ItemID) : null;
                            slotList[i].InitializeSlot(itemData, el.Amount, ownerEntity, i, inventoryType);
                        }
                        else { slotList[i].ClearSlot(); }
                    }
                }
                else { ClearAllSlots(ownerEntity, slotList, inventoryType); }
                break;

            case InventoryType.Output:
                if (em.HasBuffer<OutputInventorySlot>(ownerEntity))
                {
                    var buffer = em.GetBuffer<OutputInventorySlot>(ownerEntity);
                    for (int i = 0; i < slotList.Count; i++)
                    {
                        if (i < buffer.Length)
                        {
                            var el = buffer[i];
                            var itemData = el.ItemID != 0 ? itemRegistry.GetItemData(el.ItemID) : null;
                            slotList[i].InitializeSlot(itemData, el.Amount, ownerEntity, i, inventoryType);
                        }
                        else { slotList[i].ClearSlot(); }
                    }
                }
                else { ClearAllSlots(ownerEntity, slotList, inventoryType); }
                break;

            case InventoryType.WIP:
                if (em.HasBuffer<WorkshopWIPBufferElement>(ownerEntity))
                {
                    var buffer = em.GetBuffer<WorkshopWIPBufferElement>(ownerEntity);
                    for (int i = 0; i < slotList.Count; i++)
                    {
                        if (i < buffer.Length)
                        {
                            var el = buffer[i];
                            var itemData = el.ItemID != 0 ? itemRegistry.GetItemData(el.ItemID) : null;
                            slotList[i].InitializeSlot(itemData, el.Amount, ownerEntity, i, inventoryType);
                        }
                        else { slotList[i].ClearSlot(); }
                    }
                }
                else { ClearAllSlots(ownerEntity, slotList, inventoryType); }
                break;

            case InventoryType.General:
            default:
                if (em.HasBuffer<InventoryItemElement>(ownerEntity))
                {
                    var buffer = em.GetBuffer<InventoryItemElement>(ownerEntity);
                    for (int i = 0; i < slotList.Count; i++)
                    {
                        if (i < buffer.Length)
                        {
                            var el = buffer[i];
                            var itemData = el.ItemID != 0 ? itemRegistry.GetItemData(el.ItemID) : null;
                            slotList[i].InitializeSlot(itemData, el.Amount, ownerEntity, i, inventoryType);
                        }
                        else { slotList[i].ClearSlot(); }
                    }
                }
                else { ClearAllSlots(ownerEntity, slotList, inventoryType); }
                break;
        }
    }

    /// <summary>
    /// Очистка всех UI-слотов.
    /// </summary>
    private static void ClearAllSlots(Entity ownerEntity, List<InventorySlot> slotList, InventoryType inventoryType)
    {
        for (int i = 0; i < slotList.Count; i++)
            slotList[i].InitializeSlot(null, 0, ownerEntity, i, inventoryType);
    }
}