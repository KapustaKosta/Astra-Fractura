using Unity.Entities;
using Unity.Mathematics;

// Синглтон, хранящий информацию о предмете, на который мы смотрим
public struct HoveredItem : IComponentData
{
    public Entity LogicalEntity;
    public Entity VisualEntity;
    public int ItemID;
    public int Amount;
}

// Запрос на подбор предмета
public struct PickupRequest : IComponentData
{
    public Entity Player;
    public Entity LogicalItemEntity;
}

public struct PendingDroppedVisualInit : IComponentData
{
    public float3 Position;
    public uint   Seed;
    public Entity Logical; // логическая сущность предмета
    public int    ItemID;
    public int    Amount;
}