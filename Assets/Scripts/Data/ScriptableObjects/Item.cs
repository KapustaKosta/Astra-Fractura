using Unity.Entities;
using UnityEngine;  

public enum ItemType
{
    Resource,   // Ресурсы (руда, дерево)
    Tool,       // Инструменты (кирка, топор)
    Weapon,     // Оружие
    Consumable, // Еда, аптечки
    Building,   // Строительные блоки
    Miscellaneous // Прочее
}

[System.Flags]
public enum ResourceType
{
    None = 0,
    Wood = 1,
    Stone = 2,
    Ore = 4,
    Organic = 8
}

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class Item : ScriptableObject
{
    [Header("Basic Settings")]
    public string itemName = "New Item";
    public ItemType itemType = ItemType.Resource; // Тип предмета по умолчанию
    public Sprite icon;
    [TextArea] public string description;

    [Header("Stacking")]
    [Tooltip("Максимальное количество в одном слоте (1 = не стакается)")]
    public int maxStack = 1;

    [Header("Tool Settings")]
    [Tooltip("Урон инструмента (если это кирка/топор)")]
    public float toolDamage = 10f;
    [Tooltip("Тип ресурсов, которые можно добывать (для инструментов)")]
    public ResourceType canHarvest; // Что может добывать этот инструмент
    [Header("Durability (Optional)")]
    public bool hasDurability = false;
    public int maxDurability = 100;
    [Header("Building Settings")]
    public GameObject buildingPrefab;
    public Vector2 footprintSize = new Vector2(1, 1); // Размер основания в метрах
    [Header("System Info")]
    [Tooltip("Уникальный ID для связи с ECS")]
    public int itemID;
}