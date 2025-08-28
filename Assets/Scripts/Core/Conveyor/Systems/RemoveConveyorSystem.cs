using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

namespace Conveyor
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class RemoveConveyorSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<PhysicsWorldSingleton>();
            RequireForUpdate<InConveyorMode>();
            RequireForUpdate<PlayerTag>();
        }

        protected override void OnUpdate()
        {
            var reqQuery = SystemAPI.QueryBuilder().WithAll<RemoveConveyorUnderCursorRequest>().Build();
            if (reqQuery.IsEmpty) return;

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                               .CreateCommandBuffer(World.Unmanaged);

            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var em = EntityManager;

            if (Camera.main == null)
            {
                ecb.DestroyEntity(reqQuery, EntityQueryCaptureMode.AtPlayback);
                return;
            }

            var nextToPrev = new NativeParallelHashMap<Entity, Entity>(1024, Allocator.Temp);
            foreach (var (link, seg) in SystemAPI.Query<RefRO<ConveyorLink>>().WithEntityAccess())
            {
                var next = link.ValueRO.NextSegment;
                if (next != Entity.Null)
                    nextToPrev.TryAdd(next, seg);
            }

            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            var rayInput = new RaycastInput
            {
                Start = ray.origin,
                End = ray.origin + ray.direction * 200f,
                Filter = CollisionFilter.Default
            };

            if (physicsWorld.CastRay(rayInput, out var hit))
            {
                var hitEntity = ResolveSegmentRoot(em, hit.Entity);
                if (hitEntity == Entity.Null || !em.HasComponent<ConveyorSegmentSettings>(hitEntity))
                {
                    goto Exit;
                }

                var next = Entity.Null;
                if (em.HasComponent<ConveyorLink>(hitEntity))
                    next = em.GetComponentData<ConveyorLink>(hitEntity).NextSegment;

                Entity prev = Entity.Null;
                nextToPrev.TryGetValue(hitEntity, out prev);

                Entity touchedOwner = FindChainOwner(em, hitEntity, in nextToPrev);

                using (var connectors = new NativeList<Entity>(Allocator.Temp))
                {
                    foreach (var (connRO, cE) in SystemAPI.Query<RefRO<ConveyorConnector>>().WithEntityAccess())
                    {
                        if (connRO.ValueRO.ConnectedSegment == hitEntity)
                            connectors.Add(cE);
                    }

                    for (int i = 0; i < connectors.Length; i++)
                    {
                        var cE = connectors[i];
                        if (!em.Exists(cE)) continue;

                        var conn = em.GetComponentData<ConveyorConnector>(cE);

                        Entity newSeg = (prev == Entity.Null && next != Entity.Null) ? next : Entity.Null;
                        conn.ConnectedSegment = newSeg;
                        ecb.SetComponent(cE, conn);

                        bool occupied = newSeg != Entity.Null;
                        if (occupied && !em.HasComponent<ConveyorOccupiedTag>(cE))
                            ecb.AddComponent<ConveyorOccupiedTag>(cE);
                        if (!occupied && em.HasComponent<ConveyorOccupiedTag>(cE))
                            ecb.RemoveComponent<ConveyorOccupiedTag>(cE);
                    }
                }

                if (prev != Entity.Null && em.HasComponent<ConveyorLink>(prev))
                {
                    var pl = em.GetComponentData<ConveyorLink>(prev);
                    pl.NextSegment = next;
                    ecb.SetComponent(prev, pl);
                }

                var childrenToDelete = new NativeList<Entity>(Allocator.Temp);
                if (em.HasBuffer<LinkedEntityGroup>(hitEntity))
                {
                    var leg = em.GetBuffer<LinkedEntityGroup>(hitEntity);

                    for (int i = 0; i < leg.Length; i++)
                        childrenToDelete.Add(leg[i].Value);
                    leg.Clear();
                }

                // Удаляем корневой сегмент
                ecb.DestroyEntity(hitEntity);

                // Удаляем только "безопасных" детей (не коннекторы)
                for (int i = 0; i < childrenToDelete.Length; i++)
                {
                    var childEntity = childrenToDelete[i];
                    if (em.Exists(childEntity) && !em.HasComponent<ConveyorConnector>(childEntity))
                        ecb.DestroyEntity(childEntity);
                }
                childrenToDelete.Dispose();


                if (SystemAPI.HasSingleton<ConveyorState>())
                {
                    var conveyorState = SystemAPI.GetSingleton<ConveyorState>();
                    var addItemReq = ecb.CreateEntity();
                    ecb.AddComponent(addItemReq, new AddItemRequest
                    {
                        TargetInventoryOwner = playerEntity,
                        ItemID = conveyorState.ItemID,
                        Amount = 1
                    });
                }

                if (touchedOwner != Entity.Null)
                {
                    var recalc = ecb.CreateEntity();
                    ecb.AddComponent(recalc, new RecalculateRoutesForNetworkRequest
                    {
                        SourceBuilding = touchedOwner
                    });
                }
            }

        Exit:
            ecb.DestroyEntity(reqQuery, EntityQueryCaptureMode.AtPlayback);
            nextToPrev.Dispose();
        }

        private Entity FindChainOwner(EntityManager em, Entity segment, in NativeParallelHashMap<Entity, Entity> nextToPrev)
        {
            Entity current = segment;
            int guard = 0;
            while (nextToPrev.TryGetValue(current, out Entity prev) && guard++ < 256)
            {
                current = prev;
            }

            foreach (var (conn, e) in SystemAPI.Query<RefRO<ConveyorConnector>>().WithEntityAccess())
            {
                if (conn.ValueRO.ConnectedSegment == current)
                    return conn.ValueRO.Owner;
            }
            return Entity.Null;
        }

        private static Entity ResolveSegmentRoot(EntityManager em, Entity e)
        {
            var cur = e;
            int guard = 0;
            while (cur != Entity.Null && em.Exists(cur) && guard++ < 64)
            {
                if (em.HasComponent<ConveyorSegmentSettings>(cur))
                    return cur;
                if (!em.HasComponent<Parent>(cur))
                    break;
                cur = em.GetComponentData<Parent>(cur).Value;
            }

            if (em.Exists(e) && em.HasBuffer<LinkedEntityGroup>(e))
            {
                var buf = em.GetBuffer<LinkedEntityGroup>(e);
                for (int i = 0; i < buf.Length; i++)
                {
                    var cand = buf[i].Value;
                    if (em.Exists(cand) && em.HasComponent<ConveyorSegmentSettings>(cand))
                        return cand;
                }
            }
            return Entity.Null;
        }
    }
}
