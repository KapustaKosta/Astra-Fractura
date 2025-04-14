using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    public static Inventory Instance { get; private set; }

    [Header("Inventory Settings")]
    public List<InventoryItem> items = new List<InventoryItem>();
    public int space = 20;

    [Header("Current Selection")]
    public Item selectedItem;
    private Item equippedTool;

    public delegate void OnItemChanged();
    public OnItemChanged onItemChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    public void SelectItem(Item item)
    {
        selectedItem = item;
        
        if (item.itemType == ItemType.Tool)
        {
            EquipTool(item);
        }
    }

    public Item GetEquippedTool() => equippedTool;

    public void EquipTool(Item tool)
    {
        if (items.Exists(i => i.item == tool))
        {
            equippedTool = tool;
            Debug.Log($"Экипирован инструмент: {tool.itemName}");
            onItemChanged?.Invoke();
        }
    }

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
            Debug.Log("Инвентарь полон!");
            return false;
        }

        items.Add(new InventoryItem(item, amount));
        onItemChanged?.Invoke();
        return true;
    }

    public void Remove(Item item, int amount = 1)
    {
        InventoryItem inventoryItem = items.Find(i => i.item == item);
        if (inventoryItem == null) return;

        inventoryItem.amount -= amount;
        if (inventoryItem.amount <= 0)
            items.Remove(inventoryItem);

        onItemChanged?.Invoke();
    }

    public bool HasItem(Item item, int amount = 1)
    {
        InventoryItem invItem = items.Find(i => i.item == item);
        return invItem != null && invItem.amount >= amount;
    }
}