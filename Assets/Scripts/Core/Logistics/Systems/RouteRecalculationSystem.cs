using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Conveyor
{
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

            // Собираем ориентированный граф: segment -> NextSegment
            var neighborsMap = new NativeParallelMultiHashMap<Entity, Entity>(1024, Allocator.Temp);
            foreach (var (link, segEntity) in SystemAPI.Query<ConveyorLink>().WithEntityAccess())
            {
                if (link.NextSegment != Entity.Null)
                    neighborsMap.Add(segEntity, link.NextSegment);
            }

            foreach (var (request, requestEntity) in SystemAPI.Query<RecalculateRoutesForNetworkRequest>().WithEntityAccess())
            {
                var sourceBuilding = request.SourceBuilding;

                // Снимок существующих маршрутов от sourceBuilding: destBuilding -> routeEntity
                var existingRoutes = new NativeParallelHashMap<Entity, Entity>(32, Allocator.Temp);
                foreach (var (routeDef, routeEntity) in SystemAPI.Query<RouteDefinition>().WithEntityAccess())
                {
                    if (!connectorLookup.HasComponent(routeDef.StartConnector)) continue;
                    if (!connectorLookup.HasComponent(routeDef.EndConnector)) continue;

                    var startOwner = connectorLookup[routeDef.StartConnector].Owner;
                    if (startOwner != sourceBuilding) continue;

                    var destOwner = connectorLookup[routeDef.EndConnector].Owner;
                    existingRoutes.TryAdd(destOwner, routeEntity);
                }

                // Стартовые сегменты — ТОЛЬКО из Out-коннекторов исходного здания
                var startSegments = new NativeList<Entity>(Allocator.Temp);
                var segmentToStartConnMap = new NativeParallelHashMap<Entity, Entity>(32, Allocator.Temp);
                foreach (var (cc, connEntity) in SystemAPI.Query<ConveyorConnector>().WithEntityAccess())
                {
                    if (cc.Owner == sourceBuilding &&
                        cc.Type == ConveyorConnectorType.Out &&
                        cc.ConnectedSegment != Entity.Null)
                    {
                        if (!startSegments.Contains(cc.ConnectedSegment))
                            startSegments.Add(cc.ConnectedSegment);

                        segmentToStartConnMap.TryAdd(cc.ConnectedSegment, connEntity);
                    }
                }

                // За один пересчёт — не более 1 маршрута на каждое здание-назначение
                var processedDestinations = new NativeParallelHashSet<Entity>(16, Allocator.Temp);

                // BFS от каждого старта
                foreach (var startSeg in startSegments)
                {
                    if (!segmentToStartConnMap.TryGetValue(startSeg, out var startConnEntity))
                        continue;

                    var q = new NativeQueue<Entity>(Allocator.Temp);
                    var parent = new NativeParallelHashMap<Entity, Entity>(256, Allocator.Temp);
                    q.Enqueue(startSeg);
                    parent.Add(startSeg, Entity.Null);

                    while (q.TryDequeue(out var cur))
                    {
                        // Конец — только входной коннектор другого ЗДАНИЯ (не сегмента)
                        foreach (var (endCc, endConnEntity) in SystemAPI.Query<ConveyorConnector>().WithEntityAccess())
                        {
                            if (endCc.ConnectedSegment != cur) continue;
                            if (endCc.Type != ConveyorConnectorType.In) continue;
                            if (endCc.Owner == sourceBuilding) continue;
                            if (SystemAPI.HasComponent<ConveyorSegmentSettings>(endCc.Owner)) continue; // <— ключевое

                            var destBuilding = endCc.Owner;

                            // дедуп по назначению
                            if (!processedDestinations.Add(destBuilding))
                                continue;

                            // Восстановить путь cur -> ... -> startSeg
                            var tmp = new NativeList<Entity>(Allocator.Temp);
                            var s = cur;
                            while (s != Entity.Null)
                            {
                                tmp.Add(s);
                                parent.TryGetValue(s, out s);
                            }

                            if (existingRoutes.TryGetValue(destBuilding, out var routeEntity))
                            {
                                Debug.Log($"<color=cyan>[Routes]</color> Обновляем {sourceBuilding} -> {destBuilding}");

                                var buf = ecb.SetBuffer<RoutePathElement>(routeEntity);
                                buf.Clear();
                                for (int i = tmp.Length - 1; i >= 0; i--)
                                    buf.Add(new RoutePathElement { SegmentEntity = tmp[i] });

                                existingRoutes.Remove(destBuilding); // чтобы не удалить как "осиротевший"
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
                                var buf = ecb.AddBuffer<RoutePathElement>(newRoute);
                                for (int i = tmp.Length - 1; i >= 0; i--)
                                {
                                    var seg = tmp[i];
                                    buf.Add(new RoutePathElement { SegmentEntity = seg });
                                    ecb.AddComponent(seg, new BelongsToRoute { RouteEntity = newRoute });
                                }
                            }

                            tmp.Dispose();
                        }

                        // соседи по направлению
                        if (neighborsMap.ContainsKey(cur))
                        {
                            bool usedEnumerator = false;
#if !UNITY_DISABLE_MULTIHASHMAP_ENUM
                            try
                            {
                                foreach (var n in neighborsMap.GetValuesForKey(cur))
                                {
                                    if (n != Entity.Null && !parent.ContainsKey(n))
                                    {
                                        q.Enqueue(n);
                                        parent.Add(n, cur);
                                    }
                                }
                                usedEnumerator = true;
                            }
                            catch { }
#endif
                            if (!usedEnumerator)
                            {
                                if (neighborsMap.TryGetFirstValue(cur, out var v, out var it))
                                {
                                    do
                                    {
                                        var n = v;
                                        if (n != Entity.Null && !parent.ContainsKey(n))
                                        {
                                            q.Enqueue(n);
                                            parent.Add(n, cur);
                                        }
                                    }
                                    while (neighborsMap.TryGetNextValue(out v, ref it));
                                }
                            }
                        }
                    }

                    q.Dispose();
                    parent.Dispose();
                }

                // Удаляем неиспользованные (осиротевшие) маршруты
                foreach (var orphan in existingRoutes.GetValueArray(Allocator.Temp))
                {
                    Debug.Log($"<color=yellow>[Routes]</color> Удаляем устаревший маршрут {orphan}");
                    if (routePathLookup.HasBuffer(orphan))
                    {
                        var path = routePathLookup[orphan];
                        for (int i = 0; i < path.Length; i++)
                        {
                            var seg = path[i].SegmentEntity;
                            if (SystemAPI.HasComponent<BelongsToRoute>(seg))
                                ecb.RemoveComponent<BelongsToRoute>(seg);
                        }
                    }
                    ecb.DestroyEntity(orphan);
                }

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

