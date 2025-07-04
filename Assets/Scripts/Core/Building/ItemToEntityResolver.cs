using Unity.Entities;
using UnityEngine;
using Unity.Collections;

/// <summary>
/// Предоставляет утилитарный метод для разрешения ECS-префаба сущности по ID предмета.
/// Это используется для связывания данных MonoBehaviour-предметов с ECS-префабами.
/// </summary>
public static class ItemToEntityResolver
{
    /// <summary>
    /// Получает ECS-префаб сущности, связанный с заданным ID предмета.
    /// </summary>
    /// <param name="em">EntityManager для выполнения запросов.</param>
    /// <param name="itemID">ID предмета, для которого нужно найти префаб сущности.</param>
    /// <returns>Сущность-префаб или Entity.Null, если префаб не найден.</returns>
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

        // Debug.LogError($"Entity prefab for ItemID {itemID} not found.");
        return Entity.Null;
    }
}