using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Collections;

/// <summary>
/// Статический класс, предоставляющий утилитарные методы для выполнения
/// физических запросов, связанных с искусственным интеллектом.
/// </summary>
public static class AIPhysicsQuery
{
    /// <summary>
    /// Находит ближайшую сущность с компонентом ResourceNode в заданном радиусе.
    /// </summary>
    /// <param name="position">Центральная точка для поиска.</param>
    /// <param name="searchRadius">Радиус, в котором будет выполняться поиск.</param>
    /// <param name="collisionWorld">Физический мир, в котором производится запрос.</param>
    /// <param name="filter">Фильтр коллизий для отбора только определенных объектов (например, по слою).</param>
    /// <param name="resourceNodeLookup">Ссылка для быстрой проверки наличия компонента ResourceNode у найденной сущности.</param>
    /// <returns>Сущность ближайшего узла ресурсов или Entity.Null, если узел не найден.</returns>
    public static Entity FindNearestResource(
        float3 position,
        float searchRadius,
        in CollisionWorld collisionWorld,
        CollisionFilter filter,
        in ComponentLookup<ResourceNode> resourceNodeLookup)
    {
        var input = new PointDistanceInput
        {
            Position = position,
            MaxDistance = searchRadius,
            Filter = filter
        };

        // Выполняем запрос на поиск ближайшего объекта, соответствующего фильтру
        if (collisionWorld.CalculateDistance(input, out DistanceHit closestHit))
        {
            // Проверяем, является ли найденный объект узлом ресурсов
            if (resourceNodeLookup.HasComponent(closestHit.Entity))
            {
                return closestHit.Entity;
            }
        }
        
        // Если ничего не найдено, возвращаем пустую сущность
        return Entity.Null;
    }
}