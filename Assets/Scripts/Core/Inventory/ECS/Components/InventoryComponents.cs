using Unity.Entities;

/// <summary>
/// Представляет один стак предметов в инвентаре.
/// Хранится в динамическом буфере на сущности, у которой есть инвентарь.
/// </summary>
[System.Serializable]
public struct InventoryItemElement : IBufferElementData
{
    /// <summary>
    /// Уникальный идентификатор предмета, ссылающийся на ItemRegistry.
    /// </summary>
    public int ItemID;

    /// <summary>
    /// Количество предметов в этом стаке.
    /// </summary>
    public int Amount;
}

/// <summary>
/// Компонент-тег, указывающий, что у сущности есть инвентарь.
/// Позволяет легко запрашивать все сущности с инвентарями.
/// </summary>
public struct HasInventoryTag : IComponentData { }

/// <summary>
/// Компонент, хранящий общие свойства инвентаря, такие как его вместимость.
/// </summary>
public struct InventoryProperties : IComponentData
{
    /// <summary>
    /// Максимальное количество слотов в инвентаре.
    /// </summary>
    public int Capacity;
}