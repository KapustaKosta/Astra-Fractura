using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace Wiring
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ExitWirePlacementModeSystem))] // Работаем после выхода из режима
    public partial class WireCleanupSystem : SystemBase
    {
        // Константа цвета для сброса
        static readonly float4 kColorDefault = new float4(0.5f, 0.5f, 0.5f, 1f);

        protected override void OnUpdate()
        {
            // Если мы все еще в режиме проводов, ничего не делаем.
            if (SystemAPI.HasSingleton<InWirePlacementMode>())
            {
                return;
            }

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                               .CreateCommandBuffer(World.Unmanaged);

            // 1. Сбрасываем цвет у всех коннекторов
            foreach (var color in SystemAPI.Query<RefRW<URPMaterialPropertyBaseColor>>().WithAll<WireConnector>())
            {
                color.ValueRW.Value = kColorDefault;
            }

            // 2. Сбрасываем цвет у всех визуалов проводов
            foreach (var color in SystemAPI.Query<RefRW<URPMaterialPropertyBaseColor>>().WithAll<WireVisualTag>())
            {
                color.ValueRW.Value = kColorDefault;
            }

            // 3. Уничтожаем любые оставшиеся запросы на удаление или создание
            var pendingRemovalQuery = SystemAPI.QueryBuilder().WithAll<PendingWireRemoval>().Build();
            ecb.DestroyEntity(pendingRemovalQuery, EntityQueryCaptureMode.AtPlayback);

            var pendingPlacementQuery = SystemAPI.QueryBuilder().WithAll<PendingWire>().Build();
            ecb.DestroyEntity(pendingPlacementQuery, EntityQueryCaptureMode.AtPlayback);
        }
    }
}