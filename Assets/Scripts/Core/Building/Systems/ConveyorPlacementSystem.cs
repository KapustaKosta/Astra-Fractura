using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using UnityEngine;
using Unity.Collections;
using System.Collections.Generic;

/// <summary>
/// Система, расширяющая валидацию и размещение для конвееров, интегрированная с BuildingPlacementSystem.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BuildingPlacementSystem))]
public partial class ConveyorPlacementSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Проверяем, есть ли превью конвеера
        if (!SystemAPI.TryGetSingletonEntity<BuildingPreviewTag>(out var previewEntity)) return;
        if (!SystemAPI.Exists(previewEntity)) return;

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        var settings = SystemAPI.GetSingleton<BuildingSettings>();

        // Поиск дочерних сущностей превью, чтобы найти ConveyorBeltTag и ConveyorEndpoint
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
            // Нет дочерних сущностей ? не конвеер
            return;
        }

        // Ищем ConveyorBeltTag среди дочерних
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

        // Ищем все ConveyorEndpoint среди дочерних
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

        // Поиск ближайших валидных точек входа/выхода зданий
        bool validConnection = false;
        bool validAngle = false;
        float maxBend = 25f; // Можно вынести в настройки
        float debugAngle = 0f;
        for (int i = 0; i < previewEndpoints.Count; i++)
        {
            Entity previewEndpointEntity = previewEndpoints[i];
            ConveyorEndpoint previewEndpoint = previewEndpointData[i];
            float3 previewPos = SystemAPI.GetComponent<LocalToWorld>(previewEndpointEntity).Position;

            // Поиск ближайших точек для этого endpoint-а
            Entity closestOutput = Entity.Null;
            Entity closestInput = Entity.Null;
            float maxStartDist = 3f;
            float maxEndDist = 3f;
            foreach (var (endpoint, entity) in SystemAPI.Query<RefRO<ConveyorEndpoint>>().WithEntityAccess())
            {
                if (endpoint.ValueRO.ParentEntity == previewEndpoint.ParentEntity) continue;
                float3 endpointGlobalPos = SystemAPI.GetComponent<LocalToWorld>(entity).Position;
                float dist = math.distance(endpointGlobalPos, previewPos);
                if (!endpoint.ValueRO.IsInput && dist < maxStartDist)
                {
                    closestOutput = entity;
                }
                if (endpoint.ValueRO.IsInput && dist < maxEndDist)
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
            // Проверка на существование targetEntity
            if (targetEntity == Entity.Null || !SystemAPI.Exists(targetEntity))
                continue;
            var targetLocalToWorld = SystemAPI.GetComponent<LocalToWorld>(targetEntity);
            targetPos = targetLocalToWorld.Position;
            targetVec = targetLocalToWorld.Forward;
            targetVec.y = 0;
            targetVec = math.normalize(targetVec);

            debugAngle = math.degrees(math.acos(math.clamp(math.dot(previewVec, targetVec), -1f, 1f)));
            bool localValidAngle = math.abs(debugAngle - 90f) <= maxBend;

            // если уже есть SnapToEndpointTag, но мышка ушла далеко, снимаем тег (гистерезис)
            float snapEnableThreshold = 2.5f; // включать snapping, если ближе
            float snapDisableThreshold = 3f;  // снимать snapping, если дальше

            // Получаем world-позицию мыши через raycast (как в BuildingPlacementSystem)
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
                    if (mouseDist > snapDisableThreshold)
                    {
                        ecb.RemoveComponent<SnapToEndpointTag>(previewEntity);
                        Debug.Log($"[ConveyorPlacementSystem] SNAPPING DISABLED BY MOUSE: mouseDist={mouseDist}, snapDisableThreshold={snapDisableThreshold}");
                        return;
                    }
                }
            }

            if (localValidAngle)
            {
                validConnection = true;
                validAngle = true;

                // --- Проверка на пересечение с препятствиями (overlap) перед snapping ---
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
                            // Смещаем по XZ в позицию snapping-а, Y оставляем как есть
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
                Debug.Log($"[ConveyorPlacementSystem] endpointDist={endpointDist}, mouseDist={mouseDist}, snapEnableThreshold={snapEnableThreshold}, noOverlap={noOverlap}");
                if (endpointDist < snapEnableThreshold && mouseDist < snapEnableThreshold && noOverlap)
                {
                    if (!SystemAPI.HasComponent<SnapToEndpointTag>(previewEntity))
                    {
                        ecb.AddComponent<SnapToEndpointTag>(previewEntity);
                        Debug.Log($"[ConveyorPlacementSystem] SNAPPING ENABLED: preview snapped to endpoint at {targetPos}, dist={endpointDist}");
                    }
                    // Сам snapping: смещаем превью так, чтобы previewPos совпал с targetPos
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

        // Добавляем/удаляем PlacementValidTag/PlacementInvalidTag
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
