using Unity.Entities;

// Добавляется на ВИЗУАЛ предмета, чтобы он знал свою логическую сущность.
public struct VisualFor : IComponentData
{
    public Entity LogicalEntity;
}

// Добавляется на ЛОГИЧЕСКУЮ сущность, чтобы она знала свой визуал.
public struct LogicalItemHasVisual : IComponentData
{
    public Entity VisualEntity;
}