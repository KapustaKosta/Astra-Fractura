using Energy.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Conveyor
{
    /// <summary>
    /// Ключевая система, отвечающая за обнаружение и формирование логических маршрутов по сети конвейерных сегментов.
    /// Она запускается по запросу (`RecalculateRoutesForNetworkRequest`), как правило, после строительства или сноса конвейеров.
    /// Система выполняет поиск в ширину (BFS) по графу конвейерных сегментов, начиная от выходных (Out) коннекторов
    /// исходного здания, чтобы найти все достижимые входные (In) коннекторы других зданий.
    /// 
    /// По результатам поиска система:
    /// 1. Создает новые сущности-маршруты для ранее не существовавших путей.
    /// 2. Обновляет пути для уже существующих маршрутов, если их конфигурация изменилась.
    /// 3. Удаляет "осиротевшие" маршруты, пути которых больше не являются валидными.
    /// 4. Для каждого маршрута заполняет буферы с путем (`RoutePathElement`) и точками стыков (`RouteJoint`) для корректного движения предметов.
    /// 5. Добавляет новым маршрутам все необходимые компоненты для работы с логикой и энергосистемой.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PostBuildConnectorSystem))]
    public partial struct RouteRecalculationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RecalculateRoutesForNetworkRequest>();
        }

        public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        var connectorLookup = SystemAPI.GetComponentLookup<ConveyorConnector>(true);
        var routePathLookup = SystemAPI.GetBufferLookup<RoutePathElement>(true);
        var ltwLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);
        var lengthLookup = SystemAPI.GetComponentLookup<ConveyorSegmentRuntimeLength>(true);

        var neighborsMap = new NativeParallelMultiHashMap<Entity, Entity>(1024, Allocator.Temp);
        foreach (var (link, segEntity) in SystemAPI.Query<ConveyorLink>().WithEntityAccess())
        {
            if (link.NextSegment != Entity.Null)
                neighborsMap.Add(segEntity, link.NextSegment);
        }

        foreach (var (request, requestEntity) in SystemAPI.Query<RecalculateRoutesForNetworkRequest>().WithEntityAccess())
        {
            var sourceBuilding = request.SourceBuilding;

            var existingRoutes = new NativeParallelHashMap<Entity, Entity>(32, Allocator.Temp);
            foreach (var (routeDef, routeEntity) in SystemAPI.Query<RouteDefinition>().WithEntityAccess())
            {
                if (!connectorLookup.HasComponent(routeDef.StartConnector) || !connectorLookup.HasComponent(routeDef.EndConnector)) continue;
                var startOwner = connectorLookup[routeDef.StartConnector].Owner;
                if (startOwner != sourceBuilding) continue;
                var destOwner = connectorLookup[routeDef.EndConnector].Owner;
                existingRoutes.TryAdd(destOwner, routeEntity);
            }

            var startSegments = new NativeList<Entity>(Allocator.Temp);
            var segmentToStartConnMap = new NativeParallelHashMap<Entity, Entity>(32, Allocator.Temp);
            foreach (var (cc, connEntity) in SystemAPI.Query<ConveyorConnector>().WithEntityAccess())
            {
                if (cc.Owner == sourceBuilding && cc.Type == ConveyorConnectorType.Out && cc.ConnectedSegment != Entity.Null)
                {
                    if (!startSegments.Contains(cc.ConnectedSegment)) startSegments.Add(cc.ConnectedSegment);
                    segmentToStartConnMap.TryAdd(cc.ConnectedSegment, connEntity);
                }
            }

            var processedDestinations = new NativeParallelHashSet<Entity>(16, Allocator.Temp);

            foreach (var startSeg in startSegments)
            {
                if (!segmentToStartConnMap.TryGetValue(startSeg, out var startConnEntity)) continue;

                var q = new NativeQueue<Entity>(Allocator.Temp);
                var parent = new NativeParallelHashMap<Entity, Entity>(256, Allocator.Temp);
                q.Enqueue(startSeg);
                parent.Add(startSeg, Entity.Null);

                while (q.TryDequeue(out var cur))
                {
                    foreach (var (endCc, endConnEntity) in SystemAPI.Query<ConveyorConnector>().WithEntityAccess())
                    {
                        if (endCc.ConnectedSegment != cur) continue;
                        if (endCc.Type != ConveyorConnectorType.In) continue;
                        if (endCc.Owner == sourceBuilding) continue;
                        if (SystemAPI.HasComponent<ConveyorSegmentSettings>(endCc.Owner)) continue;

                        var destBuilding = endCc.Owner;
                        if (!processedDestinations.Add(destBuilding)) continue;

                        var pathSegmentsReversed = new NativeList<Entity>(Allocator.Temp);
                        var s = cur;
                        while (s != Entity.Null)
                        {
                            pathSegmentsReversed.Add(s);
                            parent.TryGetValue(s, out s);
                        }

                        void PopulateRouteBuffers(Entity routeEntity, bool isNew)
                        {
                            var pathBuf = isNew ? ecb.AddBuffer<RoutePathElement>(routeEntity) : ecb.SetBuffer<RoutePathElement>(routeEntity);
                            pathBuf.Clear();
                            for (int i = pathSegmentsReversed.Length - 1; i >= 0; i--)
                            {
                                var seg = pathSegmentsReversed[i];
                                pathBuf.Add(new RoutePathElement { SegmentEntity = seg });
                                if (isNew) ecb.AddComponent(seg, new BelongsToRoute { RouteEntity = routeEntity });
                            }

                            var centers = new NativeList<float3>(pathSegmentsReversed.Length, Allocator.Temp);
                            for (int i = pathSegmentsReversed.Length - 1; i >= 0; i--)
                            {
                                var seg = pathSegmentsReversed[i];
                                if (ltwLookup.HasComponent(seg))
                                    centers.Add(ltwLookup[seg].Position);
                                else
                                    centers.Add(float3.zero);
                            }

                            var jointBuf = isNew ? ecb.AddBuffer<RouteJoint>(routeEntity) : ecb.SetBuffer<RouteJoint>(routeEntity);
                            jointBuf.Clear();

                            if (centers.Length == 0) { centers.Dispose(); return; }

                            float3 GetSegmentAxisOriented(int idx)
                            {
                                var segEnt = pathBuf[idx].SegmentEntity;
                                float3 axis = new float3(0, 0, 1);
                                if (ltwLookup.HasComponent(segEnt))
                                    axis = math.normalizesafe(ltwLookup[segEnt].Forward);

                                if (centers.Length >= 2)
                                {
                                    float3 wish;
                                    if (idx < centers.Length - 1) wish = centers[idx + 1] - centers[idx];
                                    else wish = centers[idx] - centers[idx - 1];
                                    wish = math.normalizesafe(wish);
                                    if (math.dot(axis, wish) < 0f) axis = -axis;
                                }
                                return axis;
                            }

                            {
                                int i = 0;
                                var segEnt = pathBuf[i].SegmentEntity;
                                float3 center = centers[i];
                                float halfLen = 0.5f;
                                if (lengthLookup.HasComponent(segEnt))
                                    halfLen = math.max(0f, lengthLookup[segEnt].Value) * 0.5f;

                                float3 axis = GetSegmentAxisOriented(i);
                                float3 startPt = center - axis * halfLen;
                                jointBuf.Add(new RouteJoint { Position = startPt });
                            }

                            for (int i = 0; i < centers.Length; i++)
                            {
                                var segEnt = pathBuf[i].SegmentEntity;
                                float3 center = centers[i];

                                float halfLen = 0.5f;
                                if (lengthLookup.HasComponent(segEnt))
                                    halfLen = math.max(0f, lengthLookup[segEnt].Value) * 0.5f;

                                float3 axis = GetSegmentAxisOriented(i);
                                float3 endPt = center + axis * halfLen;

                                jointBuf.Add(new RouteJoint { Position = endPt });

                                if (i < centers.Length - 1)
                                    jointBuf.Add(new RouteJoint { Position = endPt });
                            }
                            centers.Dispose();
                        }

                        if (existingRoutes.TryGetValue(destBuilding, out var routeEntity))
                        {
                            Debug.Log($"<color=cyan>[Routes]</color> Обновляем {sourceBuilding} -> {destBuilding}");
                            PopulateRouteBuffers(routeEntity, false);
                            existingRoutes.Remove(destBuilding);
                        }
                        else
                        {
                            Debug.Log($"<color=green>[Routes]</color> Создаём {sourceBuilding} -> {destBuilding}");
                            var newRoute = ecb.CreateEntity();
                            ecb.AddComponent(newRoute, new RouteDefinition
                            {
                                StartConnector = startConnEntity,
                                EndConnector = endConnEntity,
                                ItemID = 0
                            });

                            ecb.AddComponent<ConveyorEnergyDemand>(newRoute);
                            ecb.AddComponent<ConsumerLoad>(newRoute);
                            ecb.AddComponent<NetworkNode>(newRoute);
                            ecb.AddComponent<NetLinkUsage>(newRoute);
                            ecb.AddComponent(newRoute, new RoutePowerStatus { PowerRatio = 0f });
                            ecb.AddComponent<NeedsEnergySetupTag>(newRoute);
                            PopulateRouteBuffers(newRoute, true);
                        }
                        pathSegmentsReversed.Dispose();
                    }

                    if (neighborsMap.ContainsKey(cur))
                    {
                        var neighbors = neighborsMap.GetValuesForKey(cur);
                        foreach (var n in neighbors)
                        {
                            if (n != Entity.Null && !parent.ContainsKey(n))
                            {
                                q.Enqueue(n);
                                parent.Add(n, cur);
                            }
                        }
                    }
                }
                q.Dispose();
                parent.Dispose();
            }

            var orphans = existingRoutes.GetValueArray(Allocator.Temp);
            for (int i = 0; i < orphans.Length; i++)
            {
                var orphan = orphans[i];
                Debug.Log($"<color=yellow>[Routes]</color> Удаляем устаревший маршрут {orphan}");
                if (routePathLookup.HasBuffer(orphan))
                {
                    var path = routePathLookup[orphan];
                    for (int j = 0; j < path.Length; j++)
                    {
                        if (SystemAPI.HasComponent<BelongsToRoute>(path[j].SegmentEntity))
                            ecb.RemoveComponent<BelongsToRoute>(path[j].SegmentEntity);
                    }
                }
                ecb.DestroyEntity(orphan);
            }
            orphans.Dispose();

            ecb.DestroyEntity(requestEntity);
            startSegments.Dispose();
            segmentToStartConnMap.Dispose();
            existingRoutes.Dispose();
            processedDestinations.Dispose();
        }
        neighborsMap.Dispose();
        ecb.Playback(state.EntityManager);
    }
}
}