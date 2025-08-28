using Unity.Entities;
using UnityEngine;

/// <summary>
/// Предоставляет утилитарный метод для разрешения ECS-префаба сущности по ID предмета.
/// </summary>
public static class ItemToEntityResolver
{
    /// <summary>
    /// Получает ECS-префаб сущности, связанный с заданным ID предмета.
    /// Метод сначала ищет специальный ВИЗУАЛЬНЫЙ префаб. Если он не найден,
    /// ищет СТРОИТЕЛЬНЫЙ префаб.
    /// </summary>
    /// <param name="em">Менеджер сущностей (EntityManager) для выполнения запросов.</param>
    /// <param name="itemID">Уникальный ID предмета, для которого ищется ECS-префаб.</param>
    /// <returns>
    /// Возвращает ECS-префаб сущности, если найден; в противном случае возвращает <see cref="Entity.Null"/>
    /// и выводит ошибку в консоль.
    /// </returns>
    public static Entity GetEntityPrefabFromID(EntityManager em, int itemID)
    {
        // этап 1: Поиск в новом реестре визуальных префабов 
        var visualQuery = em.CreateEntityQuery(typeof(ItemVisualPrefabReference));
        if (!visualQuery.IsEmpty)
        {
            using var visualEntities = visualQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            foreach (var entity in visualEntities)
            {
                var data = em.GetComponentData<ItemVisualPrefabReference>(entity);
                if (data.ItemID == itemID)
                {
                    // Нашли специальный визуал, возвращаем его
                    return data.EntityPrefab;
                }
            }
        }

        // этап 2: Если визуал не найден, ищем в старом реестре строительных префабов 
        var buildingQuery = em.CreateEntityQuery(typeof(BuildingPrefabReference));
        if (!buildingQuery.IsEmpty)
        {
            using var buildingEntities = buildingQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            foreach (var entity in buildingEntities)
            {
                var data = em.GetComponentData<BuildingPrefabReference>(entity);
                if (data.ItemID == itemID)
                {
                    // Нашли строительный префаб, возвращаем его
                    return data.EntityPrefab;
                }
            }
        }

#if UNITY_EDITOR
        Debug.LogWarning($"[ItemToEntityResolver] Не удалось найти префаб для ItemID: {itemID}. " +
                         $"Поиск прошел и по визуальным, и по строительным префабам, но совпадение не найдено.");
#endif
        return Entity.Null;
    }
}