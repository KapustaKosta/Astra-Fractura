using Unity.Entities;

public struct SpawnHybridBuildingTag : IComponentData
{
    /// <summary>
    /// Идентификатор строения (itemID), определяющий, какой префаб будет использован для спавна.
    /// Используется для поиска соответствующего префаба в массиве buildingPrefabs HybridBuildingSpawner.
    /// </summary>
    public int BuildingItemID;
}
