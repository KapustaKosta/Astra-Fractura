using Energy.Core;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace Game.Workshop
{
    /// <summary>
    /// Система, отвечающая за деактивацию производственного процесса в цехе.
    /// Срабатывает при запросе на остановку или при невозможности выполнения плана.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct WorkshopDeactivationSystem : ISystem
    {
        /// <summary>
        /// Основной метод обновления. Находит активные цеха, которые нужно остановить,
        /// снимает с них тег ProductionActiveTag, сбрасывает состояние всех их станков
        /// (статус, прогресс, назначения рабочих) и очищает очередь производства.
        /// </summary>
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            var nameLookup = SystemAPI.GetComponentLookup<NetworkNode>(true);
            var stationStateLookup = SystemAPI.GetComponentLookup<StationState>();

            foreach (var (slots, entity) in SystemAPI.Query<DynamicBuffer<StationSlot>>()
                .WithAll<WorkshopTag, ProductionActiveTag>()
                .WithAny<RequestHaltProduction, ProductionPlanIsUnfeasibleTag>()
                .WithEntityAccess())
            {
                ecb.RemoveComponent<ProductionActiveTag>(entity);

                foreach (var slot in slots)
                {
                    if (stationStateLookup.HasComponent(slot.Station))
                    {
                        var st = stationStateLookup.GetRefRW(slot.Station);
                        st.ValueRW.Status = StationStatus.Offline;
                        st.ValueRW.RemainingTime = 0;
                        st.ValueRW.AppliedHammerCost = 0;
                        st.ValueRW.AssignedWorker = Entity.Null;
                        st.ValueRW.SpecificWorker = Entity.Null;
                    }
                }

                if (SystemAPI.HasComponent<RequestHaltProduction>(entity))
                {
                    SystemAPI.GetBuffer<WorkshopProductionQueueItem>(entity).Clear();
                    ecb.RemoveComponent<RequestHaltProduction>(entity);
                }

                if (SystemAPI.HasComponent<ProductionPlanIsUnfeasibleTag>(entity))
                {
                    ecb.RemoveComponent<ProductionPlanIsUnfeasibleTag>(entity);
                }
            }

            foreach (var (tag, entity) in SystemAPI.Query<RefRO<RequestHaltProduction>>().WithNone<ProductionActiveTag>().WithEntityAccess())
            {
                ecb.RemoveComponent<RequestHaltProduction>(entity);
            }
        }
    }
}