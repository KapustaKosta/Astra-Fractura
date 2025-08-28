using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Спавнит GameObject-здания по ECS-запросам.
/// </summary>
public class HybridBuildingSpawner : MonoBehaviour
{
    [System.Serializable]
    public class BuildingPrefabEntry
    {
        public Item item;
        public GameObject prefab;
    }

    [Header("Настройки спавна")]
    public BuildingPrefabEntry[] buildingPrefabs;

    private EntityManager entityManager;
    private EntityQuery spawnRequestQuery;

    void Start()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated)
        {
            Debug.LogError("[HybridBuildingSpawner] Нет мира ECS.");
            enabled = false;
            return;
        }

        entityManager = world.EntityManager;
        spawnRequestQuery = entityManager.CreateEntityQuery(
            typeof(SpawnHybridBuildingTag),
            typeof(LocalTransform)
        );
    }

    void Update()
    {
        if (spawnRequestQuery.IsEmpty)
            return;

        using var entitiesToSpawn = spawnRequestQuery.ToEntityArray(Allocator.Temp);

        foreach (var entity in entitiesToSpawn)
        {
            var spawnData = entityManager.GetComponentData<SpawnHybridBuildingTag>(entity);
            var transformData = entityManager.GetComponentData<LocalTransform>(entity);

            GameObject prefabToSpawn = null;
            foreach (var entry in buildingPrefabs)
            {
                if (entry != null && entry.item != null && entry.item.itemID == spawnData.BuildingItemID)
                {
                    prefabToSpawn = entry.prefab;
                    break;
                }
            }

            if (prefabToSpawn == null)
            {
                Debug.LogWarning($"[HybridBuildingSpawner] Нет префаба для ItemID={spawnData.BuildingItemID}");
                entityManager.RemoveComponent<SpawnHybridBuildingTag>(entity);
                continue;
            }

            Instantiate(prefabToSpawn, transformData.Position, transformData.Rotation);
            entityManager.RemoveComponent<SpawnHybridBuildingTag>(entity);
        }
    }
}
