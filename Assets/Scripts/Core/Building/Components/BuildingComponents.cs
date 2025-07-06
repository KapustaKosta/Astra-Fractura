using Unity.Entities;

/// <summary>
/// Тег, который добавляется к "призраку" здания в режиме строительства,
/// чтобы его можно было легко найти в мире.
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
/// Тег, указывающий, что превью-сущность нуждается в первичной настройке (например, коллайдера).
/// </summary>
public struct NeedsPreviewSetupTag : IComponentData {}