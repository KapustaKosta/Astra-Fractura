using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Conveyor
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    // ВАЖНО: подтверждаем только ПОСЛЕ того, как прошла валидация текущего кадра
    [UpdateAfter(typeof(ConveyorValidationSystem))]
    public partial class ConfirmConveyorPlacementSystem : SystemBase
    {
        protected override void OnCreate() { RequireForUpdate<GameState>(); }

        protected override void OnUpdate()
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                               .CreateCommandBuffer(World.Unmanaged);
            var em = EntityManager;

            foreach (var (req, entity) in SystemAPI
                     .Query<RefRO<ConfirmConveyorPlacementRequest>>()
                     .WithEntityAccess())
            {
                var r = req.ValueRO;

                // sanity
                if (r.PreviewHolder == Entity.Null || !em.Exists(r.PreviewHolder) ||
                    !em.HasBuffer<ConveyorPathPoint>(r.PreviewHolder))
                {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                // ЖЁСТКАЯ ОТБРАКОВКА: если превью невалидно — просто гасим запрос
                if (!em.HasComponent<ConveyorPlacementValidTag>(r.PreviewHolder))
                {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                var path = em.GetBuffer<ConveyorPathPoint>(r.PreviewHolder);

                var locked = new NativeList<float3>(Allocator.Temp);
                float3 last = new float3(float.NaN, float.NaN, float.NaN);

                for (int i = 0; i < path.Length; i++)
                {
                    var p = path[i];
                    if (p.IsLocked == 0) continue;

                    if (!math.any(math.isnan(last)))
                    {
                        float d2 = math.lengthsq(new float2(p.Position.x - last.x, p.Position.z - last.z));
                        if (d2 < 1e-6f) continue;
                    }

                    locked.Add(p.Position);
                    last = p.Position;
                }

                if (locked.Length < 2)
                {
                    locked.Dispose();
                    ecb.DestroyEntity(entity);
                    continue;
                }

                ecb.AddComponent(entity, new ConveyorBuildFromPathRequest
                {
                    ItemID = r.ItemID,
                    PreviewHolder = r.PreviewHolder,
                    StartConnector = r.StartConnector,
                    EndConnector = r.EndConnector
                });

                var buildBuf = ecb.AddBuffer<ConveyorBuildPathPoint>(entity);
                for (int i = 0; i < locked.Length; i++)
                    buildBuf.Add(new ConveyorBuildPathPoint { Position = locked[i] });

                locked.Dispose();
                ecb.RemoveComponent<ConfirmConveyorPlacementRequest>(entity);
            }
        }
    }
}
