using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Collections;
using Unity.Transforms; 

/// <summary>
/// Статический класс, предоставляющий утилитарные методы для выполнения
/// физических запросов, связанных с искусственным интеллектом.
/// </summary>
public static class AIPhysicsQuery
{
    /// <summary>
    /// Находит ближайшую сущность с компонентом ResourceNode в заданном радиусе,
    /// используя OverlapAabb для большей надежности.
    /// </summary>
    /// <param name="position">Центральная точка для поиска.</param>
    /// <param name="searchRadius">Радиус, в котором будет выполняться поиск.</param>
    /// <param name="collisionWorld">Физический мир, в котором производится запрос.</param>
    /// <param name="filter">Фильтр коллизий для отбора только определенных объектов (например, по слою).</param>
    /// <param name="resourceNodeLookup">Ссылка для быстрой проверки наличия компонента ResourceNode.</param>
    /// <param name="ltwLookup">Ссылка для получения позиции найденной сущности.</param>
    /// <returns>Сущность ближайшего узла ресурсов или Entity.Null, если узел не найден.</returns>
    public static Entity FindNearestResource(
        float3 position,
        float searchRadius,
        in CollisionWorld collisionWorld,
        CollisionFilter filter,
        in ComponentLookup<ResourceNode> resourceNodeLookup,
        in ComponentLookup<LocalToWorld> ltwLookup) 
    {
        Entity bestEntity = Entity.Null;
        float minDistanceSq = searchRadius * searchRadius;

        var aabb = new Aabb
        {
            Min = position - new float3(searchRadius),
            Max = position + new float3(searchRadius)
        };

        var bodyIndices = new NativeList<int>(Allocator.Temp);
        try
        {
            var input = new OverlapAabbInput { Aabb = aabb, Filter = filter };
            if (collisionWorld.OverlapAabb(input, ref bodyIndices))
            {
                foreach (var bodyIndex in bodyIndices)
                {
                    var body = collisionWorld.Bodies[bodyIndex];
                    if (!resourceNodeLookup.HasComponent(body.Entity) || !ltwLookup.HasComponent(body.Entity))
                    {
                        continue;
                    }

                    float3 targetPos = ltwLookup[body.Entity].Position;
                    float distSq = math.distancesq(position, targetPos);

                    if (distSq < minDistanceSq)
                    {
                        minDistanceSq = distSq;
                        bestEntity = body.Entity;
                    }
                }
            }
        }
        finally
        {
            if (bodyIndices.IsCreated)
                bodyIndices.Dispose();
        }

        return bestEntity;
    }
}