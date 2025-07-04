using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Класс, представляющий элемент инвентаря, содержащий ссылку на предмет
/// (ScriptableObject) и его количество.
/// </summary>
[System.Serializable]
public class InventoryItem
{
    /// <summary>
    /// Ссылка на ScriptableObject, представляющий предмет.
    /// </summary>
    public Item item;

    /// <summary>
    /// Количество данного предмета.
    /// </summary>
    public int amount;

    /// <summary>
    /// Конструктор для создания нового элемента инвентаря.
    /// </summary>
    /// <param name="item">Предмет для добавления.</param>
    /// <param name="amount">Начальное количество предмета (по умолчанию 1).</param>
    public InventoryItem(Item item, int amount = 1)
    {
        this.item = item;
        this.amount = amount;
    }
}