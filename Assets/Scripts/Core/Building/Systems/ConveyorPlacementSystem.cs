using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using UnityEngine;
using Unity.Collections;
using System.Collections.Generic;

/// <summary>
/// Система, расширяющая валидацию и размещение для конвееров, интегрированная с BuildingPlacementSystem.
/// <para>
/// - Находит превью конвеера и его дочерние сущности (ConveyorBeltTag, ConveyorEndpoint).
/// - Для каждого endpoint превью ищет ближайшие валидные точки входа/выхода зданий (endpoints), пропуская занятые.
/// - Проверяет угол соединения, расстояние, отсутствие пересечений с препятствиями (overlap).
/// - Управляет snapping превью к endpoint-у с гистерезисом по мыши и расстоянию.
/// - Добавляет/удаляет SnapToEndpointTag, PlacementValidTag, PlacementInvalidTag.
/// </para>
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BuildingPlacementSystem))]
public partial class ConveyorPlacementSystem : SystemBase
{
    /// <summary>
    /// Максимально допустимый изгиб (градусы) между конвеерами для соединения.
    /// </summary>
    const float MaxBendDegrees = 25f;
    /// <summary>
    /// Дистанция, при которой включается snapping к endpoint.
    /// </summary>
    const float SnapEnableThreshold = 2.5f;
    /// <summary>
    /// Дистанция, при которой snapping отключается (гистерезис).
    /// </summary>
    const float SnapDisableThreshold = 3f;
    /// <summary>
    /// Максимальная дистанция поиска output endpoint.
    /// </summary>
    const float MaxStartDist = 3f;
    /// <summary>
    /// Максимальная дистанция поиска input endpoint.
    /// </summary>
    const float MaxEndDist = 3f;
    protected override void OnUpdate()
    {
        /*
         * Логика метода OnUpdate:
         * 1. Проверяет наличие превью конвеера (BuildingPreviewTag).
         * 2. Находит дочерние сущности превью (через LinkedEntityGroup или Child).
         * 3. Ищет ConveyorBeltTag и ConveyorEndpoint среди дочерних.
         * 4. Для каждого endpoint превью ищет ближайшие валидные точки входа/выхода зданий (endpoints), пропуская занятые.
         * 5. Проверяет угол соединения, расстояние, отсутствие пересечений с препятствиями (overlap).
         * 6. Управляет snapping превью к endpoint-у с гистерезисом по мыши и расстоянию.
         * 7. Добавляет/удаляет SnapToEndpointTag, PlacementValidTag, PlacementInvalidTag.
         */
        // ...existing code...
        if (!SystemAPI.TryGetSingletonEntity<BuildingPreviewTag>(out var previewEntity)) return;
        if (!SystemAPI.Exists(previewEntity)) return;

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        var settings = SystemAPI.GetSingleton<BuildingSettings>();

        List<Entity> previewChildren = null;
        if (SystemAPI.HasBuffer<LinkedEntityGroup>(previewEntity))
        {
            var buffer = SystemAPI.GetBuffer<LinkedEntityGroup>(previewEntity);
            previewChildren = new List<Entity>(buffer.Length);
            foreach (var entry in buffer)
                previewChildren.Add(entry.Value);
        }
        else if (SystemAPI.HasBuffer<Child>(previewEntity))
        {
            var buffer = SystemAPI.GetBuffer<Child>(previewEntity);
            previewChildren = new List<Entity>(buffer.Length);
            foreach (var entry in buffer)
                previewChildren.Add(entry.Value);
        }
        else
        {
            return;
        }

        Entity beltEntity = Entity.Null;
        foreach (var child in previewChildren)
        {
            if (SystemAPI.HasComponent<ConveyorBeltTag>(child))
            {
                beltEntity = child;
                break;
            }
        }
        if (beltEntity == Entity.Null) return;

        List<Entity> previewEndpoints = new List<Entity>();
        List<ConveyorEndpoint> previewEndpointData = new List<ConveyorEndpoint>();
        foreach (var child in previewChildren)
        {
            if (SystemAPI.HasComponent<ConveyorEndpoint>(child))
            {
                previewEndpoints.Add(child);
                previewEndpointData.Add(SystemAPI.GetComponent<ConveyorEndpoint>(child));
            }
        }
        if (previewEndpoints.Count == 0) return;


        bool validConnection = false;
        bool validAngle = false;
        float debugAngle = 0f;
        for (int i = 0; i < previewEndpoints.Count; i++)
        {
            Entity previewEndpointEntity = previewEndpoints[i];
            ConveyorEndpoint previewEndpoint = previewEndpointData[i];
            float3 previewPos = SystemAPI.GetComponent<LocalToWorld>(previewEndpointEntity).Position;

            Entity closestOutput = Entity.Null;
            Entity closestInput = Entity.Null;
            foreach (var (endpoint, entity) in SystemAPI.Query<RefRO<ConveyorEndpoint>>().WithEntityAccess())
            {
                if (endpoint.ValueRO.ParentEntity == previewEndpoint.ParentEntity) continue;
                if (SystemAPI.HasComponent<ConveyorEndpointOccupiedTag>(entity)) continue;
                float3 endpointGlobalPos = SystemAPI.GetComponent<LocalToWorld>(entity).Position;
                float dist = math.distance(endpointGlobalPos, previewPos);
                if (!endpoint.ValueRO.IsInput && dist < MaxStartDist)
                {
                    closestOutput = entity;
                }
                if (endpoint.ValueRO.IsInput && dist < MaxEndDist)
                {
                    closestInput = entity;
                }
            }

            bool localValidConnection = closestOutput != Entity.Null || closestInput != Entity.Null;
            if (!localValidConnection) continue;

            var previewTransform = SystemAPI.GetComponent<LocalTransform>(previewEndpointEntity);
            var previewLocalToWorld = SystemAPI.GetComponent<LocalToWorld>(previewEndpointEntity);
            float3 previewVec = previewLocalToWorld.Forward;
            previewVec.y = 0;
            previewVec = math.normalize(previewVec);

            Entity targetEntity;
            float3 targetPos = float3.zero;
            float3 targetVec = float3.zero;
            Entity targetParent = Entity.Null;
            if (previewEndpoint.IsInput)
            {
                targetEntity = closestOutput;
            }
            else
            {
                targetEntity = closestInput;
            }
            if (targetEntity == Entity.Null || !SystemAPI.Exists(targetEntity))
                continue;
            var targetLocalToWorld = SystemAPI.GetComponent<LocalToWorld>(targetEntity);
            targetPos = targetLocalToWorld.Position;
            targetVec = targetLocalToWorld.Forward;
            targetVec.y = 0;
            targetVec = math.normalize(targetVec);

            debugAngle = math.degrees(math.acos(math.clamp(math.dot(previewVec, targetVec), -1f, 1f)));
            bool localValidAngle = math.abs(debugAngle - 90f) <= MaxBendDegrees;

            float3 mouseWorldPos = float3.zero;
            bool gotMouseWorld = false;
            if (Camera.main != null)
            {
                var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
                uint buildableSurfaceLayerMask = (uint)settings.BuildableSurfaceLayerMask;
                var rayInput = new RaycastInput
                {
                    Start = ray.origin,
                    End = ray.origin + ray.direction * settings.MaxPlacementDistance,
                    Filter = new CollisionFilter { BelongsTo = ~0u, CollidesWith = buildableSurfaceLayerMask, GroupIndex = 0 }
                };
                if (physicsWorld.CollisionWorld.CastRay(rayInput, out var hit))
                {
                    mouseWorldPos = hit.Position;
                    gotMouseWorld = true;
                }
            }
            if (SystemAPI.HasComponent<SnapToEndpointTag>(previewEntity))
            {
                if (gotMouseWorld)
                {
                    float mouseDist = math.distance(targetPos, mouseWorldPos);
                    if (mouseDist > SnapDisableThreshold)
                    {
                        ecb.RemoveComponent<SnapToEndpointTag>(previewEntity);
                        Debug.Log($"[ConveyorPlacementSystem] SNAPPING DISABLED BY MOUSE: mouseDist={mouseDist}, snapDisableThreshold={SnapDisableThreshold}");
                        return;
                    }
                }
            }

            if (localValidAngle)
            {
                validConnection = true;
                validAngle = true;

                bool noOverlap = true;
                if (previewChildren != null)
                {
                    var rootTransform = SystemAPI.GetComponent<LocalTransform>(previewEntity);
                    uint obstacleLayerMask = (uint)settings.ObstacleLayerMask;
                    var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
                    foreach (var child in previewChildren)
                    {
                        if (SystemAPI.HasComponent<PhysicsCollider>(child) && SystemAPI.HasComponent<LocalTransform>(child))
                        {
                            var collider = SystemAPI.GetComponent<PhysicsCollider>(child);
                            var childTransform = SystemAPI.GetComponent<LocalTransform>(child);
                            float3 overlapCheckPos = new float3(
                                childTransform.Position.x + (targetPos.x - rootTransform.Position.x),
                                childTransform.Position.y,
                                childTransform.Position.z + (targetPos.z - rootTransform.Position.z)
                            );
                            var aabb = collider.Value.Value.CalculateAabb(new RigidTransform(Unity.Mathematics.quaternion.identity, overlapCheckPos));
                            var overlapInput = new OverlapAabbInput
                            {
                                Aabb = aabb,
                                Filter = new CollisionFilter { BelongsTo = ~0u, CollidesWith = obstacleLayerMask, GroupIndex = 0 }
                            };
                            var overlappingBodies = new NativeList<int>(Allocator.Temp);
                            bool childNoOverlap = !physicsWorld.CollisionWorld.OverlapAabb(overlapInput, ref overlappingBodies);
                            overlappingBodies.Dispose();
                            if (!childNoOverlap)
                            {
                                noOverlap = false;
                                break;
                            }
                        }
                    }
                }

                float endpointDist = math.distance(targetPos, previewPos);
                float mouseDist = gotMouseWorld ? math.distance(targetPos, mouseWorldPos) : float.MaxValue;
                Debug.Log($"[ConveyorPlacementSystem] endpointDist={endpointDist}, mouseDist={mouseDist}, snapEnableThreshold={SnapEnableThreshold}, noOverlap={noOverlap}");
                if (endpointDist < SnapEnableThreshold && mouseDist < SnapEnableThreshold && noOverlap)
                {
                    if (!SystemAPI.HasComponent<SnapToEndpointTag>(previewEntity))
                    {
                        ecb.AddComponent<SnapToEndpointTag>(previewEntity);
                        Debug.Log($"[ConveyorPlacementSystem] SNAPPING ENABLED: preview snapped to endpoint at {targetPos}, dist={endpointDist}");
                    }
                    if (SystemAPI.HasComponent<LocalTransform>(previewEntity))
                    {
                        var rootTransform = SystemAPI.GetComponentRW<LocalTransform>(previewEntity);
                        float3 offset = targetPos - previewPos;
                        offset.y = 0;
                        rootTransform.ValueRW.Position += offset;
                    }
                }
                else
                {
                    if (SystemAPI.HasComponent<SnapToEndpointTag>(previewEntity))
                    {
                        ecb.RemoveComponent<SnapToEndpointTag>(previewEntity);
                        Debug.Log($"[ConveyorPlacementSystem] SNAPPING DISABLED: preview unsnapped, dist={endpointDist}");
                    }
                }
                break;
            }
        }

        if (validConnection && validAngle)
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
