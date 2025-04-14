using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class InventoryItem
{
    public Item item;  // Ссылка на ScriptableObject предмета
    public int amount; // Количество

    public InventoryItem(Item item, int amount = 1)
    {
        this.item = item;
        this.amount = amount;
    }
}