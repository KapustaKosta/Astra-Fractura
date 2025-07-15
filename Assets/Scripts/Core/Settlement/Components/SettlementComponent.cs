using Unity.Entities;
using Unity.Collections;

/// <summary>
/// ECS-компонент, хранящий все данные, относящиеся к одному поселению.
/// </summary>
public struct SettlementComponent : IComponentData
{
    /// <summary>
    /// Название поселения.
    /// </summary>
    public FixedString64Bytes Name;

    /// <summary>
    /// Текущее количество жителей поселения.
    /// </summary>
    public int Population;

    /// <summary>
    /// Текущий уровень поселения.
    /// </summary>
    public int Level;

    /// <summary>
    /// Список сущностей NPC, приписанных к этому поселению.
    /// </summary>
    public FixedList64Bytes<Entity> NPCs;
}

/// <summary>
/// Компонент-тег, указывающий, что в инвентаре поселения
/// нет ни одного свободного слота. Управляется системой SettlementInventoryStateSystem.
/// </summary>
public struct SettlementInventoryFullTag : IComponentData { }