using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Тег, который добавляется к "призраку" здания в режиме строительства.
/// </summary>
public struct BuildingPreviewTag : IComponentData { }

/// <summary>
/// Тег, добавляемый к превью-сущности, когда ее можно разместить в текущей позиции.
/// </summary>
public struct PlacementValidTag : IComponentData { }

/// <summary>
/// Тег, добавляемый к превью-сущности, когда ее нельзя разместить в текущей позиции.
/// </summary>
public struct PlacementInvalidTag : IComponentData { }

/// <summary>
/// Тег, указывающий, что превью-сущность нуждается в первичной настройке.
/// </summary>
public struct NeedsPreviewSetupTag : IComponentData { }

/// <summary>
/// Компонент, хранящий локальное смещение для точки прибытия NPC.
/// </summary>
public struct ArrivalPointOffset : IComponentData
{
    public float3 Value;
}

/// <summary>
/// Хранит локальное смещение от pivot'а объекта до его фактического основания (самой нижней точки).
/// </summary>
public struct BuildingPivotOffset : IComponentData
{
    public float3 Value;
}