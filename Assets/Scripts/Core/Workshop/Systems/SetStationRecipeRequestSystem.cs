using Unity.Burst;
using Unity.Entities;

namespace Game.Workshop
{
    /// <summary>
    /// Система для обработки запросов на установку или изменение рецепта для конкретной станции.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(WorkshopActivationSystem))]
    public partial struct SetStationRecipeRequestSystem : ISystem
    {
        /// <summary>
        /// Основной метод обновления. Находит все запросы SetStationRecipeRequest,
        /// валидирует их, обновляет SelectedRecipeID у целевой станции и сбрасывает ее прогресс.
        /// </summary>
        [BurstCompile]
        public void OnUpdate(ref SystemState s)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(s.WorldUnmanaged);
            foreach (var (req, ent) in SystemAPI.Query<RefRO<SetStationRecipeRequest>>().WithEntityAccess())
            {
                if (!SystemAPI.Exists(req.ValueRO.Workshop) || !SystemAPI.HasBuffer<StationSlot>(req.ValueRO.Workshop))
                { ecb.DestroyEntity(ent); continue; }

                ecb.AddComponent<WorkshopChainChangedTag>(req.ValueRO.Workshop);

                var slots = SystemAPI.GetBuffer<StationSlot>(req.ValueRO.Workshop);
                if (req.ValueRO.SlotIndex < 0 || req.ValueRO.SlotIndex >= slots.Length) { ecb.DestroyEntity(ent); continue; }

                var stEnt = slots[req.ValueRO.SlotIndex].Station;
                if (SystemAPI.Exists(stEnt) && SystemAPI.HasComponent<StationState>(stEnt))
                {
                    var st = SystemAPI.GetComponent<StationState>(stEnt);
                    if (st.SelectedRecipeID != req.ValueRO.RecipeID)
                    {
                        st.SelectedRecipeID = req.ValueRO.RecipeID;
                        st.RemainingTime = 0;
                        st.AppliedHammerCost = 0;
                        st.TimePenalty = 0;

                        if (st.Status != StationStatus.Empty && st.Status != StationStatus.NeedsRepair)
                        {
                            st.Status = st.Enabled == 1 ? StationStatus.Idle : StationStatus.Offline;
                        }
                    }
                    SystemAPI.SetComponent(stEnt, st);
                }
                ecb.DestroyEntity(ent);
            }
        }
    }
}