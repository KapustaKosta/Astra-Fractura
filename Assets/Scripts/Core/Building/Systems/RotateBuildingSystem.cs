using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

/// <summary>
/// Система, которая обрабатывает запросы на поворот здания в режиме строительства.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BuildingPlacementSystem))]
public partial class RotateBuildingSystem : SystemBase
{
    private const float ROTATE_SPEED = 2.5f; // градусов за кадр
    protected override void OnUpdate()
    {
        // Получаем сущность превью здания
        if (!SystemAPI.TryGetSingletonEntity<BuildingPreviewTag>(out var previewEntity)) return;

        // Проверяем наличие запроса на поворот
        foreach (var reqEntity in SystemAPI.QueryBuilder().WithAll<RotateRequest>().Build().ToEntityArray(Unity.Collections.Allocator.Temp))
        {
            // Поворачиваем превью здания
            if (SystemAPI.HasComponent<LocalTransform>(previewEntity))
            {
                var transform = SystemAPI.GetComponentRW<LocalTransform>(previewEntity);
                transform.ValueRW.Rotation = math.mul(transform.ValueRW.Rotation, quaternion.RotateY(math.radians(ROTATE_SPEED)));
            }
            // Удаляем только компонент RotateRequest
            EntityManager.RemoveComponent<RotateRequest>(reqEntity);
        }
    }
}
