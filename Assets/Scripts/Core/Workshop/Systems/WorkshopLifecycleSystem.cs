using Energy.Core;
using Game.Production;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace Game.Workshop
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(WorkshopPlannerSystem))]
    public partial struct WorkshopLifecycleSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            var nameLookup = SystemAPI.GetComponentLookup<NetworkNode>(true);
            var stationStateLookup = SystemAPI.GetComponentLookup<StationState>();

            // СЦЕНАРИЙ 1: Активация производства
            foreach (var (tag, entity) in SystemAPI.Query<RefRO<WorkshopTag>>()
                .WithAll<ProductionPlanIsFeasibleTag>()
                .WithNone<ProductionActiveTag>()
                .WithEntityAccess())
            {
                ecb.AddComponent(entity, new ProductionActiveTag());
                var name = nameLookup.HasComponent(entity) ? nameLookup[entity].Name.ToString() : "Unknown";
                Debug.Log($"[Lifecycle] Activating production for '{name}'.");
            }

            // СЦЕНАРИЙ 2: Остановка производства
            foreach (var (slots, entity) in SystemAPI.Query<DynamicBuffer<StationSlot>>()
                .WithAll<WorkshopTag, ProductionActiveTag>()
                .WithAny<RequestHaltProduction, ProductionPlanIsUnfeasibleTag>()
                .WithEntityAccess())
            {
                var name = nameLookup.HasComponent(entity) ? nameLookup[entity].Name.ToString() : "Unknown";
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
                    }
                }

                if (SystemAPI.HasComponent<RequestHaltProduction>(entity))
                {
                    SystemAPI.GetBuffer<WorkshopProductionQueueItem>(entity).Clear();
                    Debug.LogWarning($"[Lifecycle] HALTING production for '{name}' due to explicit request.");
                }
                else
                {
                    Debug.LogWarning($"[Lifecycle] PAUSING production for '{name}' due to unfeasible plan.");
                }
            }

            // СЦЕНАРИЙ 3: Очистка обработанных временных тегов и запросов
            foreach (var (tag, entity) in SystemAPI.Query<RefRO<RequestHaltProduction>>().WithEntityAccess())
            {
                ecb.RemoveComponent<RequestHaltProduction>(entity);
            }
            foreach (var (tag, entity) in SystemAPI.Query<RefRO<ProductionPlanIsFeasibleTag>>().WithEntityAccess())
            {
                ecb.RemoveComponent<ProductionPlanIsFeasibleTag>(entity);
            }
            foreach (var (tag, entity) in SystemAPI.Query<RefRO<ProductionPlanIsUnfeasibleTag>>().WithEntityAccess())
            {
                ecb.RemoveComponent<ProductionPlanIsUnfeasibleTag>(entity);
            }
        }
    }
}