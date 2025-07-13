using Unity.Entities;
using UnityEngine;

/// <summary>
/// Предоставляет утилитарный метод для разрешения ECS-префаба сущности по ID предмета.
/// </summary>
public static class ItemToEntityResolver
{
    /// <summary>
    /// Получает ECS-префаб сущности, связанный с заданным ID предмета.
    /// Метод ищет зарегистрированный BuildingPrefabReference, соответствующий указанному ID предмета,
    /// и возвращает связанный с ним ECS-префаб.
    /// </summary>
    /// <param name="em">Менеджер сущностей (EntityManager) для выполнения запросов.</param>
    /// <param name="itemID">Уникальный ID предмета, для которого ищется ECS-префаб.</param>
    /// <returns>
    /// Возвращает ECS-префаб сущности, если найден; в противном случае возвращает <see cref="Entity.Null"/>
    /// и выводит ошибку в консоль.
    /// </returns>
    public static Entity GetEntityPrefabFromID(EntityManager em, int itemID)
    {
        // Создаем запрос для поиска всех сущностей, содержащих компонент BuildingPrefabReference.
        var query = em.CreateEntityQuery(typeof(BuildingPrefabReference));
        
        // Преобразуем результат запроса в NativeArray для безопасной итерации.
        using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);

        
        foreach (var entity in entities)
        {
            // Получаем компонент BuildingPrefabReference для текущей сущности.
            var data = em.GetComponentData<BuildingPrefabReference>(entity);
            // Если ItemID из компонента совпадает с искомым, возвращаем соответствующий ECS-префаб.
            if (data.ItemID == itemID)
            {
                return data.EntityPrefab;
            }
        }
        
        
        // Если мы дошли до сюда, префаб не был найден.
        Debug.LogError($"[ItemToEntityResolver] Не удалось найти префаб сущности для ItemID: {itemID}.");
        return Entity.Null;
    }
}