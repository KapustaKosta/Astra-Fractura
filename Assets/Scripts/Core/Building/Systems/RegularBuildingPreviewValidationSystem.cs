using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
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
        if (!SystemAPI.TryGetSingletonEntity<BuildingPreviewTag>(out var preview)) return;
        if (SystemAPI.HasComponent<FoundationTag>(preview) || SystemAPI.HasComponent<SnapToEndpointTag>(preview)) return;

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        var physics = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        var settings = SystemAPI.GetSingleton<BuildingSettings>();
        var transform = SystemAPI.GetComponent<LocalTransform>(preview);

        // 1. Стандартные проверки
        bool isGroundedAndSlopeValid = IsGroundedAndSlopeValid(transform.Position, in physics, in settings);
        bool isOverlapValid = IsOverlapValid(preview, transform, in physics, in settings, ref state);

        // 2. Расширенная проверка для карьера
        bool isQuarryCheckValid = true;
        if (SystemAPI.HasComponent<QuarryPlacementTag>(preview))
        {
            isQuarryCheckValid = IsQuarryPlacementValid(preview, transform, ref state, ecb);
        }
        
        // 3. Финальное решение
        bool isPlacementValid = isGroundedAndSlopeValid && isOverlapValid && isQuarryCheckValid;
        
        // 4. Применение тегов
        if (isPlacementValid)
        {
            if (!SystemAPI.HasComponent<PlacementValidTag>(preview)) ecb.AddComponent<PlacementValidTag>(preview);
            if (SystemAPI.HasComponent<PlacementInvalidTag>(preview)) ecb.RemoveComponent<PlacementInvalidTag>(preview);
        }
        else
        {
            if (!SystemAPI.HasComponent<PlacementInvalidTag>(preview)) ecb.AddComponent<PlacementInvalidTag>(preview);
            if (SystemAPI.HasComponent<PlacementValidTag>(preview)) ecb.RemoveComponent<PlacementValidTag>(preview);
        }
    }

    private bool IsGroundedAndSlopeValid(float3 position, in PhysicsWorldSingleton physics, in BuildingSettings settings)
    {
        var rayInput = new RaycastInput
        {
            Start = position + new float3(0, 1.5f, 0), End = position - new float3(0, 3.0f, 0),
            Filter = new CollisionFilter { BelongsTo = ~0u, CollidesWith = (uint)settings.BuildableSurfaceLayerMask, GroupIndex = 0 }
        };
        if (physics.CastRay(rayInput, out PhRaycastHit hit))
        {
            return SlopeUtil.IsSlopeAllowed(hit.SurfaceNormal, settings.MaxPlacementSlopeAngle);
        }
        return false;
    }

    private bool IsOverlapValid(Entity preview, LocalTransform transform, in PhysicsWorldSingleton physics, in BuildingSettings settings, ref SystemState state)
    {
        if (!SystemAPI.HasComponent<PhysicsCollider>(preview)) return true;
        
        var collider = SystemAPI.GetComponent<PhysicsCollider>(preview);
        var aabb = collider.Value.Value.CalculateAabb(new RigidTransform(transform.Rotation, transform.Position));
        var overlapInput = new OverlapAabbInput { Aabb = aabb, Filter = new CollisionFilter { BelongsTo = ~0u, CollidesWith = (uint)settings.ObstacleLayerMask, GroupIndex = 0 } };
        

        var overlappingBodies = new NativeList<int>(Allocator.Temp);
        bool hasOverlap = physics.CollisionWorld.OverlapAabb(overlapInput, ref overlappingBodies);
        overlappingBodies.Dispose(); 
        
        return !hasOverlap;
    }

    private bool IsQuarryPlacementValid(Entity preview, LocalTransform transform, ref SystemState state, EntityCommandBuffer ecb)
{
    // Гарантируем наличие enableable-компонента перед SetComponentEnabled
    if (!SystemAPI.HasComponent<QuarryPreviewTarget>(preview))
    {
        ecb.AddComponent(preview, new QuarryPreviewTarget { TargetNode = Entity.Null });
        // По умолчанию выключен
        UnityEngine.Debug.Log($"<color=#56D2FF>[Quarry-Validate]</color> Added QuarryPreviewTarget to preview {preview.Index}");
    }

    var quarrySettings = SystemAPI.GetComponent<QuarrySettings>(preview); // радиус берём из превью
    float interactionRangeSq = quarrySettings.InteractionRange * quarrySettings.InteractionRange;

    Entity closestNode = Entity.Null;
    float closestDistSq = float.MaxValue;

    // Узлы, которые уже заняты другими карьерами
    var occupiedNodes = new Unity.Collections.NativeHashSet<Entity>(16, Unity.Collections.Allocator.Temp);
    foreach (var quarryState in SystemAPI.Query<RefRO<QuarryState>>())
        if (quarryState.ValueRO.TargetResourceNode != Entity.Null) occupiedNodes.Add(quarryState.ValueRO.TargetResourceNode);

    int candidates = 0;
    foreach (var (nodeTransform, nodeEntity) in SystemAPI.Query<RefRO<LocalToWorld>>().WithAll<ResourceNode>().WithEntityAccess())
    {
        float distSq = math.distancesq(transform.Position, nodeTransform.ValueRO.Position);
        bool inRange = distSq < interactionRangeSq;
        bool free    = !occupiedNodes.Contains(nodeEntity);

        if (inRange) candidates++;

        // DEBUG-кольцо вокруг кандидатов
        if (inRange)
            UnityEngine.Debug.DrawLine(transform.Position,
                nodeTransform.ValueRO.Position, UnityEngine.Color.cyan, 0f, false);

        if (inRange && free && distSq < closestDistSq)
        {
            closestDistSq = distSq;
            closestNode   = nodeEntity;
        }
    }
    occupiedNodes.Dispose();

    if (closestNode != Entity.Null)
    {
        // Включаем и пишем цель
        ecb.SetComponentEnabled<QuarryPreviewTarget>(preview, true);
        ecb.SetComponent(preview, new QuarryPreviewTarget { TargetNode = closestNode });

        // DEBUG
        UnityEngine.Debug.Log($"<color=#56D2FF>[Quarry-Validate]</color>" +
                              $" Found target Node={closestNode.Index}," +
                              $" candidates={candidates}, distSq={closestDistSq:F2}");
        return true;
    }

    // Нет цели — выключаем (только если уже включён)
    if (SystemAPI.IsComponentEnabled<QuarryPreviewTarget>(preview))
        ecb.SetComponentEnabled<QuarryPreviewTarget>(preview, false);

    UnityEngine.Debug.Log($"<color=#56D2FF>[Quarry-Validate]</color> " +
                          $"No target in range. candidates={candidates}");
    return false;
}
}