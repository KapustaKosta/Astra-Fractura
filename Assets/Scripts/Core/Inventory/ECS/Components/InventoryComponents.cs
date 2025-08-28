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

/// <summary>
/// Тег-компонент, который временно добавляется к сущности,
/// чей инвентарь был изменен в текущем кадре.
/// Используется для запуска систем, которые должны реагировать на изменения в инвентаре.
/// </summary>
public struct InventoryChangedTag : IComponentData { }



/// <summary>
/// Перечисление для указания, какой именно инвентарь нужно отобразить.
/// </summary>
public enum InventoryType
{
    /// <summary>
    /// Стандартный инвентарь, использующий буфер InventoryItemElement.
    /// </summary>
    General,
    /// <summary>
    /// Входной инвентарь производственного здания.
    /// </summary>
    Input,
    /// <summary>
    /// Выходной инвентарь производственного здания.
    /// </summary>
    Output,
    /// <summary>
    /// Буферный инвентарь цеха (Work-in-Progress).
    /// </summary>
    WIP
}

/// <summary>
/// Запрос на открытие UI инвентаря для указанной сущности и типа инвентаря.
/// </summary>
public struct OpenInventoryUIRequest : IComponentData
{
    public Entity Target;
    public InventoryType Type;
}


/// <summary>
/// Хранит вместимость (количество слотов) для ВХОДНОГО инвентаря.
/// </summary>
public struct InputInventoryCapacity : IComponentData
{
    public int Value;
}

/// <summary>
/// Хранит вместимость (количество слотов) для ВЫХОДНОГО инвентаря.
/// </summary>
public struct OutputInventoryCapacity : IComponentData
{
    public int Value;
}

/// <summary>
/// Хранит вместимость (количество слотов) для буферного инвентаря (WIP).
/// </summary>
public struct WIPInventoryCapacity : IComponentData
{
    public int Value;
}