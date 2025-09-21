using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine; 

using URay = UnityEngine.Ray;
using PhRaycastHit = Unity.Physics.RaycastHit;

/// <summary>
/// Система, которая позиционирует превью НЕфундаментных зданий в мире
/// в зависимости от положения мыши и ориентирует их по рельефу.
/// Также выполняет базовую проверку наклона поверхности.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BuildingHeightAdjustmentSystem))] // Убедимся, что смещение высоты применено
[UpdateBefore(typeof(RotateBuildingSystem))] // Позиционируем до поворота, так как поворот может быть применен к новой позиции
[UpdateBefore(typeof(RegularBuildingPreviewValidationSystem))] // Позиционируем до валидации
public partial class RegularBuildingPreviewPlacementSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<PhysicsWorldSingleton>();
        RequireForUpdate<BuildingPreviewTag>(); 
        RequireForUpdate<BuildingSettings>();
        // Эта система специально исключает фундаменты и здания, привязанные к конечным точкам,
        // поскольку их логика позиционирования обрабатывается в других местах.
    }

    protected override void OnUpdate()
    {
        if (!SystemAPI.TryGetSingletonEntity<BuildingPreviewTag>(out var previewEntity) || !SystemAPI.Exists(previewEntity))
            return;
        
        if (SystemAPI.HasComponent<FoundationTag>(previewEntity))
            return;
        if (SystemAPI.HasComponent<SnapToEndpointTag>(previewEntity))
            return;

        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        var settings = SystemAPI.GetSingleton<BuildingSettings>();
        var em = EntityManager;
        
        var cam = Camera.main;
        if (cam == null)
        {
            SetPlacementInvalid(previewEntity, em);
            return;
        }

        URay mainRay = cam.ScreenPointToRay(Input.mousePosition);
        var rayInput = new RaycastInput
        {
            Start = mainRay.origin,
            End = mainRay.origin + mainRay.direction * settings.MaxPlacementDistance,
            Filter = new CollisionFilter { BelongsTo = ~0u, CollidesWith = (uint)settings.BuildableSurfaceLayerMask, GroupIndex = 0 }
        };

        var lt = em.GetComponentData<LocalTransform>(previewEntity);
        bool placementPossible = false;

        if (physicsWorld.CollisionWorld.CastRay(rayInput, out PhRaycastHit mainHit))
        {
            // Теперь вызываем логику позиционирования и ориентации, аналогичную HandleRegularBuildingPlacement
            placementPossible = CalculateRegularBuildingPlacement(ref lt, previewEntity, mainHit.Position, in physicsWorld, in settings);
        }
        else
        {
            // Если главный луч не попадает ни во что, здание "висит в воздухе".
            // Его позиция останется прежней, но мы явно помечаем как невалидное.
            SetPlacementInvalid(previewEntity, em);
            return;
        }
        
        em.SetComponentData(previewEntity, lt);

        // Обновляем тег валидности на основе только что выполненной проверки наклона
        if (placementPossible)
        {
            SetPlacementValid(previewEntity, em);
        }
        else
        {
            SetPlacementInvalid(previewEntity, em);
        }
    }
    
    private bool CalculateRegularBuildingPlacement(ref LocalTransform lt, Entity previewEntity, float3 centerHitPos, in PhysicsWorldSingleton physicsWorld, in BuildingSettings settings)
    {
        var em = EntityManager;
        float2 footprintSize = em.HasComponent<BuildingFootprint>(previewEntity)
            ? em.GetComponentData<BuildingFootprint>(previewEntity).Size
            : new float2(1f, 1f);

        float3 pivotOffset = em.HasComponent<BuildingPivotOffset>(previewEntity)
            ? em.GetComponentData<BuildingPivotOffset>(previewEntity).Value
            : float3.zero;

        float2 halfSize = footprintSize * 0.5f;
        var localOffsets = new NativeArray<float3>(5, Allocator.Temp) // Центр + 4 угла
        {
            [0] = float3.zero,
            [1] = new float3(halfSize.x, 0, halfSize.y),
            [2] = new float3(halfSize.x, 0, -halfSize.y),
            [3] = new float3(-halfSize.x, 0, -halfSize.y),
            [4] = new float3(-halfSize.x, 0, halfSize.y)
        };

        var hitPoints = new NativeList<float3>(5, Allocator.Temp);
        var hitNormals = new NativeList<float3>(5, Allocator.Temp);

        // Для каждого из 5 точек делаем рейкаст вниз
        for (int i = 0; i < localOffsets.Length; i++)
        {
            // Применяем текущий поворот к локальному смещению, чтобы точки были в мировом пространстве
            float3 rotatedOffset = math.mul(lt.Rotation, localOffsets[i]);
            // Начинаем луч чуть выше предполагаемой точки попадания и стреляем вниз
            float3 rayStart = centerHitPos + rotatedOffset + new float3(0, 2.0f, 0);
            float3 rayEnd = rayStart - new float3(0, 4.0f, 0);

            var rayInput = new RaycastInput
            {
                Start = rayStart,
                End = rayEnd,
                Filter = new CollisionFilter
                {
                    BelongsTo = ~0u,
                    CollidesWith = (uint)settings.BuildableSurfaceLayerMask,
                    GroupIndex = 0
                }
            };
            
            // #if UNITY_EDITOR
            // Debug.DrawRay(rayInput.Start, rayInput.End - rayInput.Start, Color.yellow, 0.1f, true);
            // #endif

            if (physicsWorld.CollisionWorld.CastRay(rayInput, out PhRaycastHit hit))
            {
                hitPoints.Add(hit.Position);
                hitNormals.Add(hit.SurfaceNormal);
            }
        }

        localOffsets.Dispose();

        // Если не удалось найти достаточно точек для определения поверхности, размещение невозможно.
        // Оригинальный код использовал 3, сохраним это.
        if (hitPoints.Length < 3)
        {
            hitPoints.Dispose();
            hitNormals.Dispose();
            return false;
        }

        float3 avgPosition = float3.zero;
        float3 avgNormal = float3.zero;
        foreach (var p in hitPoints) avgPosition += p;
        foreach (var n in hitNormals) avgNormal += n;

        avgPosition /= hitPoints.Length;
        avgNormal = math.normalize(avgNormal);

        hitPoints.Dispose();
        hitNormals.Dispose();

        // Устанавливаем вращение, чтобы здание смотрело "вперед" относительно камеры
        // и "вверх" по нормали поверхности.
        quaternion targetRotation = quaternion.LookRotation(MathUtil.GetForwardFromRotation(lt.Rotation, avgNormal), avgNormal);

        lt.Rotation = targetRotation;
        // Устанавливаем позицию, учитывая офсет пивота
        lt.Position = avgPosition - math.mul(lt.Rotation, pivotOffset);

        // Возвращаем результат проверки наклона. Это базовая проверка,
        // полная валидация будет в RegularBuildingPreviewValidationSystem.
        return SlopeUtil.IsSlopeAllowed(avgNormal, settings.MaxPlacementSlopeAngle);
    }

    private void SetPlacementValid(Entity previewEntity, EntityManager em)
    {
        if (!em.HasComponent<PlacementValidTag>(previewEntity))
            em.AddComponentData(previewEntity, new PlacementValidTag());
        if (em.HasComponent<PlacementInvalidTag>(previewEntity))
            em.RemoveComponent<PlacementInvalidTag>(previewEntity);
    }

    private void SetPlacementInvalid(Entity previewEntity, EntityManager em)
    {
        if (!em.HasComponent<PlacementInvalidTag>(previewEntity))
            em.AddComponentData(previewEntity, new PlacementInvalidTag());
        if (em.HasComponent<PlacementValidTag>(previewEntity))
            em.RemoveComponent<PlacementValidTag>(previewEntity);
    }
}

public static class MathUtil 
{
    public static float3 GetForwardFromRotation(quaternion originalRotation, float3 surfaceNormal)
    {
        float3 originalForward = math.mul(originalRotation, new float3(0, 0, 1));
        float3 forwardOnPlane = originalForward - math.dot(originalForward, surfaceNormal) * surfaceNormal;

        if (math.lengthsq(forwardOnPlane) < 0.001f)
        {
            float3 worldForward = new float3(0, 0, 1);
            forwardOnPlane = worldForward - math.dot(worldForward, surfaceNormal) * surfaceNormal;

            if (math.lengthsq(forwardOnPlane) < 0.001f)
            {
                float3 worldRight = new float3(1, 0, 0);
                forwardOnPlane = worldRight - math.dot(worldRight, surfaceNormal) * surfaceNormal;
            }
        }
        return math.normalize(forwardOnPlane);
    }
}
