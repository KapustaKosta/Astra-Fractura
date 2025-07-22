using Unity.Entities;

/// <summary>
/// Компонент, хранящий индекс активного слота квикбара (0-7).
/// Присутствует на сущности игрока.
/// </summary>
public struct ActiveQuickbarSlot : IComponentData
{
    public int Index;
}

/// <summary>
/// Компонент, хранящий ID предмета, который в данный момент "экипирован"
/// (находится в активном слоте квикбара).
/// Если в активном слоте нет предмета, этот компонент удаляется с сущности игрока.
/// </summary>
public struct ActiveEquippedItem : IComponentData
{
    public int ItemID;
}