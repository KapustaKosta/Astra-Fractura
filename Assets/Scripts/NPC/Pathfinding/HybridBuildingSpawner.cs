using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class HybridBuildingSpawner : MonoBehaviour
{
    [System.Serializable]
    public class BuildingPrefabEntry
    {
        public Item item; 
        public GameObject prefab;
    }

    public BuildingPrefabEntry[] buildingPrefabs;

    void Update()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null)
            return;
        var entityManager = world.EntityManager;

        var query = entityManager.CreateEntityQuery(
            typeof(SpawnHybridBuildingTag), typeof(LocalTransform)
        );

        using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
        foreach (var entity in entities)
        {
            if (!entityManager.HasComponent<LocalTransform>(entity))
                continue;

            var spawnHybridBuldingTag = entityManager.GetComponentData<SpawnHybridBuildingTag>(entity);
            var transform = entityManager.GetComponentData<LocalTransform>(entity);

            int itemID = spawnHybridBuldingTag.BuildingItemID;

            GameObject prefab = null;
            foreach (var entry in buildingPrefabs)
            {
                if (entry == null || entry.item == null)
                    continue;
                if (entry.item.itemID == itemID)
                {
                    prefab = entry.prefab;
                    break;
                }
            }
            if (prefab == null)
                continue;

            var go = Instantiate(prefab, transform.Position, transform.Rotation);

            var components = go.GetComponents<Component>();
            foreach (var comp in components)
            {
                if (comp is Transform || comp is MeshRenderer || comp is MeshFilter || comp is UnityEngine.AI.NavMeshObstacle)
                    continue;
                Destroy(comp);
            }

            // (опционально) Привязать GameObject к entity, если нужно

            entityManager.RemoveComponent<SpawnHybridBuildingTag>(entity);
        }
    }
}