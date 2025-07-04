using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Управляет инвентарем игрока, включая добавление, удаление и выбор предметов.
/// Является Singleton-классом.
/// </summary>
public class Inventory : MonoBehaviour
{
    /// <summary>
    /// Singleton-экземпляр Inventory.
    /// </summary>
    public static Inventory Instance { get; private set; }

    /// <summary>
    /// Список предметов, находящихся в инвентаре.
    /// </summary>
    [Header("Inventory Settings")]
    public List<InventoryItem> items = new List<InventoryItem>();

    /// <summary>
    /// Максимальное количество слотов в инвентаре.
    /// </summary>
    public int space = 20;

    /// <summary>
    /// Текущий выбранный предмет в инвентаре.
    /// </summary>
    [Header("Current Selection")]
    public Item selectedItem;

    private Item equippedTool;

    /// <summary>
    /// Делегат для события изменения инвентаря.
    /// </summary>
    public delegate void OnItemChanged();

    /// <summary>
    /// Событие, вызываемое при изменении содержимого инвентаря.
    /// </summary>
    public OnItemChanged onItemChanged;

    /// <summary>
    /// Вызывается при загрузке скрипта. Инициализирует Singleton-экземпляр.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Устанавливает выбранный предмет. Если это инструмент, он также экипируется.
    /// </summary>
    /// <param name="item">Предмет для выбора.</param>
    public void SelectItem(Item item)
    {
        selectedItem = item;

        if (item != null && item.itemType == ItemType.Tool)
        {
            EquipTool(item);
        }
    }

    /// <summary>
    /// Возвращает текущий экипированный инструмент.
    /// </summary>
    /// <returns>Экипированный инструмент.</returns>
    public Item GetEquippedTool() => equippedTool;

    /// <summary>
    /// Экипирует указанный инструмент, если он находится в инвентаре.
    /// </summary>
    /// <param name="tool">Инструмент для экипировки.</param>
    public void EquipTool(Item tool)
    {
        if (tool != null && items.Exists(i => i.item == tool))
        {
            equippedTool = tool;
            onItemChanged?.Invoke();
        }
    }

    /// <summary>
    /// Добавляет предмет в инвентарь.
    /// </summary>
    /// <param name="item">Предмет для добавления.</param>
    /// <param name="amount">Количество добавляемых предметов (по умолчанию 1).</param>
    /// <returns>True, если предмет успешно добавлен, false в противном случае.</returns>
    public bool Add(Item item, int amount = 1)
    {
        if (item.maxStack > 1)
        {
            InventoryItem existingItem = items.Find(i => i.item == item && i.amount < item.maxStack);
            if (existingItem != null)
            {
                existingItem.amount += amount;
                onItemChanged?.Invoke();
                return true;
            }
        }

        if (items.Count >= space)
        {
            return false;
        }
        
        items.Add(new InventoryItem(item, amount));
        onItemChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Удаляет предмет из инвентаря.
    /// </summary>
    /// <param name="item">Предмет для удаления.</param>
    /// <param name="amount">Количество удаляемых предметов (по умолчанию 1).</param>
    public void Remove(Item item, int amount = 1)
    {
        InventoryItem inventoryItem = items.Find(i => i.item == item);
        if (inventoryItem == null) return;

        inventoryItem.amount -= amount;
        if (inventoryItem.amount <= 0)
            items.Remove(inventoryItem);

        onItemChanged?.Invoke();
    }

    /// <summary>
    /// Проверяет, есть ли указанный предмет в инвентаре в достаточном количестве.
    /// </summary>
    /// <param name="item">Предмет для проверки.</param>
    /// <param name="amount">Требуемое количество (по умолчанию 1).</param>
    /// <returns>True, если предмет есть в достаточном количестве, false в противном случае.</returns>
    public bool HasItem(Item item, int amount = 1)
    {
        InventoryItem invItem = items.Find(i => i.item == item);
        return invItem != null && invItem.amount >= amount;
    }

    /// <summary>
    /// Обрабатывает создание предмета по рецепту.
    /// </summary>
    /// <param name="recipe">Рецепт для создания.</param>
    public void CraftItem(CraftingRecipe recipe)
    {
        recipe.Craft(this);
    }
}