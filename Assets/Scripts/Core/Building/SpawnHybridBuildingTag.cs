using Unity.Entities;

/// <summary>
/// Тег-компонент для пометки зданий, для которых нужно создать гибридный (GameObject) prefab в сцене.
/// Используется HybridBuildingSpawner.
/// </summary>
public struct SpawnHybridBuildingTag : IComponentData
{
    public int BuildingItemID;
 }
