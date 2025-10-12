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
[UpdateAfter(typeof(BuildingHeightAdjustmentSystem))]
[UpdateBefore(typeof(RotateBuildingSystem))]
[UpdateBefore(typeof(RegularBuildingPreviewValidationSystem))]
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

        if (SystemAPI.HasComponent<FoundationTag>(previewEntity) || SystemAPI.HasComponent<SnapToEndpointTag>(previewEntity))
            return;

        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        var settings = SystemAPI.GetSingleton<BuildingSettings>();
        var em = EntityManager;

        var cam = Camera.main;
        if (cam == null) return;

        URay mainRay = cam.ScreenPointToRay(Input.mousePosition);
        var rayInput = new RaycastInput
        {
            Start = mainRay.origin,
            End = mainRay.origin + mainRay.direction * settings.MaxPlacementDistance,
            Filter = new CollisionFilter { BelongsTo = ~0u, CollidesWith = (uint)settings.BuildableSurfaceLayerMask, GroupIndex = 0 }
        };

        var lt = em.GetComponentData<LocalTransform>(previewEntity);
        bool placementPossible;

        if (physicsWorld.CollisionWorld.CastRay(rayInput, out PhRaycastHit mainHit))
        {
            // Успешный основной луч
            placementPossible = CalculateRegularBuildingPlacement(ref lt, previewEntity, mainHit.Position, in physicsWorld, in settings);
        }
        else
        {
            // Основной луч никуда не попал
            Debug.Log($"<color=cyan>[Placement]</color> Main raycast from camera did not hit any buildable surface.");
            SetPlacementInvalid(previewEntity, em, "Raycast miss");
            em.SetComponentData(previewEntity, lt);
            return;
        }

        em.SetComponentData(previewEntity, lt);
        
        if (!placementPossible)
        {
            // Если уклон плохой, ставим тег невалидности.
            SetPlacementInvalid(previewEntity, em, "Slope check failed");
        }
        else
        {
            // Если уклон хороший, мы должны УБРАТЬ тег невалидности, если он был.
            if (em.HasComponent<PlacementInvalidTag>(previewEntity))
            {
                Debug.Log($"<color=cyan>[Placement]</color> Slope check PASSED. Removing previous InvalidTag before handing over to ValidationSystem.");
                em.RemoveComponent<PlacementInvalidTag>(previewEntity);
            }
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
        var localOffsets = new NativeArray<float3>(5, Allocator.Temp)
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

            var rayInput = new RaycastInput { Start = rayStart, End = rayEnd, Filter = new CollisionFilter { BelongsTo = ~0u, CollidesWith = (uint)settings.BuildableSurfaceLayerMask, GroupIndex = 0 } };
            
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
            Debug.LogWarning($"<color=cyan>[Placement]</color> Not enough footprint points hit the ground ({hitPoints.Length} < 3). Placement is likely impossible.");
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

        lt.Position = avgPosition - math.mul(lt.Rotation, pivotOffset);
        
        bool isSlopeAllowed = SlopeUtil.IsSlopeAllowed(avgNormal, settings.MaxPlacementSlopeAngle);
        if (!isSlopeAllowed)
        {
            Debug.Log($"<color=cyan>[Placement]</color> Slope check FAILED. Surface normal: {avgNormal}, Angle > {settings.MaxPlacementSlopeAngle}");
        }

        return isSlopeAllowed;
    }

    private void SetPlacementInvalid(Entity previewEntity, EntityManager em, string reason)
    {
        if (!em.HasComponent<PlacementInvalidTag>(previewEntity))
        {
            Debug.Log($"<color=cyan>[Placement]</color> Setting state to INVALID. Reason: {reason}. Adding PlacementInvalidTag.");
            em.AddComponentData(previewEntity, new PlacementInvalidTag());
            if (em.HasComponent<PlacementValidTag>(previewEntity))
                em.RemoveComponent<PlacementValidTag>(previewEntity);
        }
    }
}