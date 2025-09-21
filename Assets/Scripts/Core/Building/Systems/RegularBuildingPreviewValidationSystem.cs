using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems; 
using Unity.Transforms;
using UnityEngine; 

using URay = UnityEngine.Ray;
using PhRaycastHit = Unity.Physics.RaycastHit; 

/// <summary>
/// Система, которая проверяет валидность размещения превью зданий (не фундаментов).
/// Проверяет отсутствие пересечений с препятствиями и полную поддержку нижней части здания.
/// Поддерживает как обычные здания, так и те, что привязываются к конечным точкам (SnapToEndpointTag).
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(RegularBuildingPreviewPlacementSystem))] // После позиционирования обычных зданий мышью
[UpdateAfter(typeof(RotateBuildingSystem))] // После применения поворота
// Она также должна запускаться после любой системы, которая устанавливает позицию для сущностей SnapToEndpointTag.
[UpdateBefore(typeof(PreviewMaterialSystem))] // Чтобы материалы могли обновляться на основе валидности
[UpdateBefore(typeof(ConfirmPlacementSystem))] // Чтобы ConfirmPlacementSystem мог проверить валидность
public partial class RegularBuildingPreviewValidationSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<PhysicsWorldSingleton>();
        RequireForUpdate<BuildingPreviewTag>(); 
        RequireForUpdate<BuildingSettings>();
    }

    protected override void OnUpdate()
    {
        if (!SystemAPI.TryGetSingletonEntity<BuildingPreviewTag>(out var previewEntity) || !SystemAPI.Exists(previewEntity))
            return;
        
        // Эта система не проверяет фундаменты; FoundationPlacementSystem обрабатывает это.
        if (SystemAPI.HasComponent<FoundationTag>(previewEntity))
            return;

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        var settings = SystemAPI.GetSingleton<BuildingSettings>();
        
        bool isPlacementValid = true; // Считаем валидным, пока не докажем обратное
        
        var currentTransform = SystemAPI.GetComponent<LocalTransform>(previewEntity);
        float3 previewPosition = currentTransform.Position;

        // Используем настроенные маски слоев для проверки коллизий.
        uint buildableSurfaceLayerMask = (uint)settings.BuildableSurfaceLayerMask;
        uint obstacleLayerMask = (uint)settings.ObstacleLayerMask; 


        bool noOverlap = true;
        bool allBottomSupported = true;

        if (SystemAPI.HasComponent<PhysicsCollider>(previewEntity))
        {
            var collider = SystemAPI.GetComponent<PhysicsCollider>(previewEntity);
            var aabb = collider.Value.Value.CalculateAabb(new RigidTransform(currentTransform.Rotation, previewPosition)); // Используем текущее вращение!

            float3 min = aabb.Min;
            float3 max = aabb.Max;

            // 1. Проверка на пересечение с препятствиями (только реальные препятствия)
            var overlapInput = new OverlapAabbInput
            {
                Aabb = aabb,
                Filter = new CollisionFilter { BelongsTo = ~0u, CollidesWith = obstacleLayerMask, GroupIndex = 0 }
            };
            var overlappingBodies = new NativeList<int>(Allocator.Temp);
            if (physicsWorld.CollisionWorld.OverlapAabb(overlapInput, ref overlappingBodies))
            {
                noOverlap = false;
                // Debug.Log($"<color=red>Validation FAILED ({previewEntity}): Overlap with {overlappingBodies.Length} obstacles.</color>");
            }
            overlappingBodies.Dispose();
            if (!noOverlap) isPlacementValid = false;

            // 2. Проверка полной поддержки нижней части (общая для всех превью, кроме фундаментов)
            float yBottomCheck = min.y + 0.01f; // Чуть выше абсолютного дна, чтобы гарантировать попадание
            float3[] bottomPoints = new float3[5]; // Углы + Центр нижней грани
            bottomPoints[0] = new float3(min.x, yBottomCheck, min.z);
            bottomPoints[1] = new float3(max.x, yBottomCheck, min.z);
            bottomPoints[2] = new float3(min.x, yBottomCheck, max.z);
            bottomPoints[3] = new float3(max.x, yBottomCheck, max.z);
            bottomPoints[4] = new float3((min.x+max.x)*0.5f, yBottomCheck, (min.z+max.z)*0.5f);

            float downCheckDepth = 0.6f; 
            float upCheckHeight = 1.0f;   // Проверка на препятствие непосредственно над точкой

            for (int i = 0; i < bottomPoints.Length; i++)
            {
                // Проверка на наличие поверхности под точкой
                var downRay = new RaycastInput
                {
                    Start = bottomPoints[i],
                    End = bottomPoints[i] + new float3(0, -downCheckDepth, 0),
                    Filter = new CollisionFilter { BelongsTo = ~0u, CollidesWith = buildableSurfaceLayerMask, GroupIndex = 0 }
                };
                
                // #if UNITY_EDITOR // Визуализация лучей в редакторе для отладки
                // Debug.DrawRay(downRay.Start, downRay.End - downRay.Start, Color.blue, 0.1f, true);
                // #endif

                if (!physicsWorld.CollisionWorld.CastRay(downRay, out PhRaycastHit _))
                {
                    allBottomSupported = false;
                    // Debug.Log($"<color=red>Validation FAILED ({previewEntity}): Point {i} at {bottomPoints[i]} has no bottom support.</color>");
                    break;
                }
                // Проверка на препятствие непосредственно над точкой поддержки (например, попытка разместить внутри земли)
                var upRay = new RaycastInput
                {
                    Start = bottomPoints[i],
                    End = bottomPoints[i] + new float3(0, upCheckHeight, 0),
                    Filter = new CollisionFilter { BelongsTo = ~0u, CollidesWith = buildableSurfaceLayerMask, GroupIndex = 0 }
                };

                // #if UNITY_EDITOR // Визуализация лучей в редакторе для отладки
                // Debug.DrawRay(upRay.Start, upRay.End - upRay.Start, Color.red, 0.1f, true);
                // #endif

                if (physicsWorld.CollisionWorld.CastRay(upRay, out PhRaycastHit _))
                {
                    allBottomSupported = false;
                    // Debug.Log($"<color=red>Validation FAILED ({previewEntity}): Point {i} at {bottomPoints[i]} is obstructed from above (inside ground).</color>");
                    break;
                }
            }
            if (!allBottomSupported) isPlacementValid = false;
        }
        else // Если у сущности превью нет PhysicsCollider, мы не можем выполнить детальные проверки.
        {
            isPlacementValid = false;
            // Debug.Log($"<color=red>Validation FAILED ({previewEntity}): Preview entity has no PhysicsCollider.</color>");
        }

        // Окончательное решение о валидности размещения
        // Debug.Log($"<color=green>Final Validity for {previewEntity}: {isPlacementValid} (Overlap: {noOverlap}, Support: {allBottomSupported})</color>");
        
        // Обновляем теги валидности размещения.
        if (isPlacementValid)
        {
            ecb.AddComponent<PlacementValidTag>(previewEntity);
            ecb.RemoveComponent<PlacementInvalidTag>(previewEntity);
        }
        else
        {
            ecb.AddComponent<PlacementInvalidTag>(previewEntity);
            ecb.RemoveComponent<PlacementValidTag>(previewEntity);
        }
    }
}