using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Conveyor
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class ConveyorPreviewAutoCleanupSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            if (SystemAPI.HasSingleton<InConveyorMode>()) return;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // 1) Снимаем подсветки
            foreach (var (tag, e) in SystemAPI.Query<RefRO<ConveyorConnectorHighlighted>>().WithEntityAccess())
                ecb.RemoveComponent<ConveyorConnectorHighlighted>(e);

            // 2) Уничтожаем все ghost-entities
            foreach (var (tag, e) in SystemAPI.Query<RefRO<ConveyorGhostTag>>().WithEntityAccess())
                ecb.DestroyEntity(e);

            // 3) Уничтожаем holder'ы
            var holderQuery = SystemAPI.QueryBuilder().WithAny<ConveyorPathPoint, ConveyorGhostFrozenRef>().Build();
            ecb.DestroyEntity(holderQuery, EntityQueryCaptureMode.AtPlayback);


            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}