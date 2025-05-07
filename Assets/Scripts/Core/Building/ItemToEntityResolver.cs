using Unity.Entities;
using UnityEngine;

public static class ItemToEntityResolver
{
    public static Entity GetEntityPrefabFromID(EntityManager em, int itemID)
    {
        var query = em.CreateEntityQuery(typeof(BuildingPrefabReference));
        using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);

        foreach (var entity in entities)
        {
            var data = em.GetComponentData<BuildingPrefabReference>(entity);
            if (data.ItemID == itemID)
            {
                return data.EntityPrefab;
            }
        }

        Debug.LogError($"Entity prefab for ItemID {itemID} not found.");
        return Entity.Null;
    }
}
