using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

/// <summary>
/// Система, которая обрабатывает запросы на поворот здания в режиме строительства.
/// <para>
/// - Получает сущность превью здания (BuildingPreviewTag).
/// - Обрабатывает все запросы на поворот (RotateRequest).
/// - Поворачивает превью здания на ROTATE_SPEED градусов по оси Y за кадр.
/// - После обработки удаляет компонент RotateRequest у соответствующих сущностей.
/// </para>
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(RegularBuildingPreviewPlacementSystem))]
public partial class RotateBuildingSystem : SystemBase
{
    /// <summary>
    /// Скорость поворота превью здания (градусов за кадр).
    /// </summary>
    private const float ROTATE_SPEED = 2.5f;
    /// <summary>
    /// Обрабатывает все запросы на поворот здания в режиме строительства.
    /// </summary>
    protected override void OnUpdate()
    {
        /*
         * 1. Получает сущность превью здания (BuildingPreviewTag).
         * 2. Обрабатывает все запросы на поворот (RotateRequest).
         * 3. Поворачивает превью здания на ROTATE_SPEED градусов по оси Y за кадр.
         * 4. После обработки удаляет компонент RotateRequest у соответствующих сущностей.
         */
        if (!SystemAPI.TryGetSingletonEntity<BuildingPreviewTag>(out var previewEntity)) return;

        foreach (var reqEntity in SystemAPI.QueryBuilder().WithAll<RotateRequest>().Build().ToEntityArray(Unity.Collections.Allocator.Temp))
        {
            if (SystemAPI.HasComponent<LocalTransform>(previewEntity))
            {
                var transform = SystemAPI.GetComponentRW<LocalTransform>(previewEntity);


                // Вращаем вокруг мировой оси Y для предсказуемого поведения на склонах.
                // quaternion.RotateY создает вращение в мировом пространстве.
                // Умножение слева (world * local) применяет мировое вращение к локальному повороту.
                transform.ValueRW.Rotation = math.mul(quaternion.RotateY(math.radians(ROTATE_SPEED)), transform.ValueRW.Rotation);
            }
            EntityManager.RemoveComponent<RotateRequest>(reqEntity);
        }
    }
}