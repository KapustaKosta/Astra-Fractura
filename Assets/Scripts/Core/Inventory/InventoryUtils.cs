using Unity.Entities;

/// <summary>
/// Статический класс-утилита, содержащий общие, переиспользуемые методы для работы с инвентарями.
/// </summary>
public static class InventoryUtils
{
    /// <summary>
    /// Проверяет, есть ли в указанном инвентаре место для заданного предмета.
    /// Место считается существующим, если есть хотя бы один пустой слот или
    /// хотя бы один неполный стак этого же предмета.
    /// </summary>
    public static bool HasSpaceForItem(
        in DynamicBuffer<InventoryItemElement> inventory, 
        int itemIDToCheck, 
        int maxStackSize)
    {
        foreach (var item in inventory)
        {
            if (item.ItemID == 0) return true;
            if (item.ItemID == itemIDToCheck && item.Amount < maxStackSize) return true;
        }
        return false;
    }

    /// <summary>
    /// Проверяет, является ли инвентарь полностью заполненным.
    /// </summary>
    public static bool IsInventoryFull(
        in DynamicBuffer<InventoryItemElement> inventory, 
        ItemRegistry itemRegistry)
    {
        if (inventory.Length == 0) return false;
        foreach (var item in inventory)
        {
            if (item.ItemID == 0) return false;
            var itemData = itemRegistry.GetItemData(item.ItemID);
            if (itemData != null && item.Amount < itemData.maxStack) return false;
        }
        return true;
    }
    
    /// <summary>
    /// Проверяет, пуст ли инвентарь.
    /// </summary>
    public static bool IsInventoryEmpty(in DynamicBuffer<InventoryItemElement> inventory)
    {
        foreach (var item in inventory)
        {
            if (item.ItemID != 0 && item.Amount > 0) return false;
        }
        return true;
    }

    /// <summary>
    /// Находит ID первого попавшегося предмета в инвентаре.
    /// </summary>
    /// <param name="inventory">Инвентарь для поиска.</param>
    /// <returns>ID первого найденного предмета или 0, если инвентарь пуст.</returns>
    public static int GetFirstItemID(in DynamicBuffer<InventoryItemElement> inventory)
    {
        foreach (var item in inventory)
        {
            if (item.ItemID != 0) return item.ItemID;
        }
        return 0;
    }

    /// <summary>
    /// Рассчитывает процент заполненности инвентаря на основе занятых слотов.
    /// </summary>
    /// <param name="inventory">Инвентарь для проверки.</param>
    /// <returns>Значение от 0.0 до 1.0, представляющее процент занятых слотов.</returns>
    public static float GetFullnessPercentage(in DynamicBuffer<InventoryItemElement> inventory)
    {
        if (!inventory.IsCreated || inventory.Length == 0) return 0f;

        int occupiedSlots = 0;
        foreach (var item in inventory)
        {
            if (item.ItemID != 0) occupiedSlots++;
        }
        return (float)occupiedSlots / inventory.Length;
    }
}