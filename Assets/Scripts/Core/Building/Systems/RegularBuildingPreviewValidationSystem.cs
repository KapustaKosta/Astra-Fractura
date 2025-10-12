using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
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
[UpdateAfter(typeof(RegularBuildingPreviewPlacementSystem))]
public partial struct RegularBuildingPreviewValidationSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PhysicsWorldSingleton>();
        state.RequireForUpdate<BuildingSettings>();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonEntity<BuildingPreviewTag>(out var preview))
            return;

        if (SystemAPI.HasComponent<FoundationTag>(preview) || SystemAPI.HasComponent<SnapToEndpointTag>(preview))
            return;

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        var physics = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        var settings = SystemAPI.GetSingleton<BuildingSettings>();
        var transform = SystemAPI.GetComponent<LocalTransform>(preview);

        // Этап 1: Сбор результатов всех проверок
        Debug.Log($"<color=orange>[Validation]</color> ===== FRAME START for entity {preview.Index} =====");

        // 1. Проверка уклона (результат из предыдущей системы)
        bool isSlopeValid = !SystemAPI.HasComponent<PlacementInvalidTag>(preview);
        Debug.Log($"<color=orange>[Validation]</color> 1. Slope Check Passed: {isSlopeValid}");

        // 2. Проверка на пересечение (Overlap Check)
        bool isOverlapValid = true;
        if (SystemAPI.HasComponent<PhysicsCollider>(preview))
        {
            var collider = SystemAPI.GetComponent<PhysicsCollider>(preview);
            var aabb = collider.Value.Value.CalculateAabb(new RigidTransform(transform.Rotation, transform.Position));
            var overlapInput = new OverlapAabbInput { Aabb = aabb, Filter = new CollisionFilter { BelongsTo = ~0u, CollidesWith = (uint)settings.ObstacleLayerMask, GroupIndex = 0 } };
            
            var overlappingBodies = new NativeList<int>(Allocator.Temp);
            if (physics.CollisionWorld.OverlapAabb(overlapInput, ref overlappingBodies))
            {
                isOverlapValid = false;
            }
            overlappingBodies.Dispose();
        }
        Debug.Log($"<color=orange>[Validation]</color> 2. Overlap Check Passed: {isOverlapValid}");

        // 3. Проверка опоры (луч вниз)
        bool isGrounded = false;
        var rayInput = new RaycastInput { Start = transform.Position + new float3(0, 2.0f, 0), End = transform.Position + new float3(0, -5.0f, 0), Filter = new CollisionFilter { BelongsTo = ~0u, CollidesWith = (uint)settings.BuildableSurfaceLayerMask, GroupIndex = 0 } };
        if (physics.CastRay(rayInput, out _))
        {
            isGrounded = true;
        }
        Debug.Log($"<color=orange>[Validation]</color> 3. Grounded Check Passed: {isGrounded}");

        // 4. Логика валидации карьера
        bool isQuarryCheckValid = true;
        bool isQuarry = SystemAPI.HasComponent<QuarryPlacementTag>(preview);
        if (isQuarry)
        {
            Debug.Log($"<color=orange>[Validation]</color> 4. Is a Quarry. Starting specific checks...");
            var quarrySettings = SystemAPI.GetComponent<QuarrySettings>(preview);
            float interactionRangeSq = quarrySettings.InteractionRange * quarrySettings.InteractionRange;

            Entity closestNode = Entity.Null;
            float closestDistSq = float.MaxValue;

            var occupiedNodes = new NativeHashSet<Entity>(16, Allocator.Temp);
            foreach (var stateRO in SystemAPI.Query<RefRO<QuarryState>>())
            {
                if (stateRO.ValueRO.TargetResourceNode != Entity.Null)
                    occupiedNodes.Add(stateRO.ValueRO.TargetResourceNode);
            }

            foreach (var (_, nodeTransform, nodeEntity) in SystemAPI.Query<ResourceNode, RefRO<LocalToWorld>>().WithEntityAccess())
            {
                if (occupiedNodes.Contains(nodeEntity)) continue;

                float distSq = math.distancesq(transform.Position, nodeTransform.ValueRO.Position);
                if (distSq < interactionRangeSq && distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    closestNode = nodeEntity;
                }
            }
            occupiedNodes.Dispose();

            if (closestNode != Entity.Null)
            {
                isQuarryCheckValid = true;
                Debug.Log($"<color=green>[Validation]</color> Quarry Check Passed. Found closest free node: {closestNode.Index}.");
                ecb.SetComponentEnabled<QuarryPreviewTarget>(preview, true);
                ecb.SetComponent(preview, new QuarryPreviewTarget { TargetNode = closestNode });
            }
            else
            {
                isQuarryCheckValid = false;
                Debug.Log($"<color=red>[Validation]</color> Quarry Check FAILED. No free resource node in range.");
                ecb.SetComponentEnabled<QuarryPreviewTarget>(preview, false);
            }
        }
        
        // Финальное решение и применение тегов
        bool isPlacementValid = isSlopeValid && isOverlapValid && isGrounded && isQuarryCheckValid;
        Debug.Log($"<color=yellow>[Validation]</color> FINAL DECISION: isPlacementValid = {isPlacementValid} (Slope:{isSlopeValid}, Overlap:{isOverlapValid}, Grounded:{isGrounded}, Quarry:{isQuarryCheckValid})");
        
        // Обновляем теги валидности размещения.
        if (isPlacementValid)
        {
            if (!SystemAPI.HasComponent<PlacementValidTag>(preview))
            {
                Debug.Log($"<color=yellow>[Validation]</color> -> Adding PlacementValidTag.");
                ecb.AddComponent<PlacementValidTag>(preview);
            }
            if (SystemAPI.HasComponent<PlacementInvalidTag>(preview))
            {
                Debug.Log($"<color=yellow>[Validation]</color> -> Removing PlacementInvalidTag.");
                ecb.RemoveComponent<PlacementInvalidTag>(preview);
            }
        }
        else
        {
            if (!SystemAPI.HasComponent<PlacementInvalidTag>(preview))
            {
                Debug.Log($"<color=yellow>[Validation]</color> -> Adding PlacementInvalidTag.");
                ecb.AddComponent<PlacementInvalidTag>(preview);
            }
            if (SystemAPI.HasComponent<PlacementValidTag>(preview))
            {
                Debug.Log($"<color=yellow>[Validation]</color> -> Removing PlacementValidTag.");
                ecb.RemoveComponent<PlacementValidTag>(preview);
            }
        }
        Debug.Log($"<color=orange>[Validation]</color> ===== FRAME END =====");
    }
}
