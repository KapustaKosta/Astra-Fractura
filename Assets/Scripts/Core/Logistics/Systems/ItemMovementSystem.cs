using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Conveyor
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(RouteTransferSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial class ItemMovementSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var endSimEcb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = endSimEcb.CreateCommandBuffer(World.Unmanaged);
            var writer = ecb.AsParallelWriter();
            
            var registryQuery = GetEntityQuery(ComponentType.ReadOnly<ItemVisualPrefabReference>());
            var registry = registryQuery.ToComponentDataArray<ItemVisualPrefabReference>(Allocator.Temp);

            var map = new NativeHashMap<int, Entity>(math.max(1, registry.Length), Allocator.TempJob);
            for (int i = 0; i < registry.Length; i++)
                map.TryAdd(registry[i].ItemID, registry[i].EntityPrefab);

            registry.Dispose();

            var pathLookup = GetBufferLookup<RoutePathElement>(true);
            var ltwLookup = GetComponentLookup<LocalToWorld>(true);
            var activeRouteLkUp = GetComponentLookup<ActiveRouteTag>(true);
            var hasTransitLkUp = GetComponentLookup<ItemInTransit>(true);

            var spawnJob = new SpawnVisualsJob
            {
                ECB = writer,
                PathLookup = pathLookup,
                LtwLookup = ltwLookup,
                ActiveRouteLookup = activeRouteLkUp,
                ItemIdToPrefabMap = map,
            };
            var hSpawn = spawnJob.ScheduleParallel(Dependency);

            var cleanupJob = new CleanupVisualsJob
            {
                ECB = writer,
                HasTransit = hasTransitLkUp
            };
            var hCleanup = cleanupJob.ScheduleParallel(hSpawn);

            map.Dispose(hCleanup);
            Dependency = hCleanup;
        }

        [BurstCompile]
        [WithNone(typeof(HasVisualTag))]
        public partial struct SpawnVisualsJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ECB;

            [ReadOnly] public BufferLookup<RoutePathElement> PathLookup;
            [ReadOnly] public ComponentLookup<LocalToWorld> LtwLookup;
            [ReadOnly] public ComponentLookup<ActiveRouteTag> ActiveRouteLookup;
            [ReadOnly] public NativeHashMap<int, Entity> ItemIdToPrefabMap;

            void Execute([ChunkIndexInQuery] int sortKey, Entity transitEntity, in ItemInTransit item)
            {
                if (item.RouteEntity == Entity.Null) return;
                if (!ActiveRouteLookup.HasComponent(item.RouteEntity)) return;
                if (!PathLookup.HasBuffer(item.RouteEntity)) return;

                var path = PathLookup[item.RouteEntity];
                if (path.Length == 0) return;

                if (!ItemIdToPrefabMap.TryGetValue(item.ItemID, out var prefab))
                {
                    return;
                }
                
                var visual = ECB.Instantiate(sortKey, prefab);

                ECB.AddComponent(sortKey, visual, new VisualFor { LogicalEntity = transitEntity });
                ECB.AddComponent<HasVisualTag>(sortKey, transitEntity);
                ECB.AddComponent<ConveyorVisualProgress>(sortKey, visual);
                ECB.AddComponent<ConveyorVisualNeedsInitTag>(sortKey, visual);
            }
        }

        [BurstCompile]
        public partial struct CleanupVisualsJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ECB;
            [ReadOnly] public ComponentLookup<ItemInTransit> HasTransit;

            void Execute([ChunkIndexInQuery] int sortKey, Entity visualEntity, in VisualFor link)
            {
                bool alive = link.LogicalEntity != Entity.Null && HasTransit.HasComponent(link.LogicalEntity);
                if (!alive)
                    ECB.DestroyEntity(sortKey, visualEntity);
            }
        }
    }
}