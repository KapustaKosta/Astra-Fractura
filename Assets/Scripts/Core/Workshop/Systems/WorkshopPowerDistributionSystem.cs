using Energy.Core;
using Energy.Core.Systems;
using Game.Production;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Workshop
{
    /// <summary>
    /// Система, распределяющая доступную энергию между работающими станками в цехе.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EnergyDispatchSystem))]
    [UpdateBefore(typeof(WorkshopStateTransitionSystem))]
    public partial struct WorkshopPowerDistributionSystem : ISystem
    {
        /// <summary>
        /// Основной метод обновления. Для каждого активного цеха получает фактически
        /// доступную мощность от энергосистемы, сравнивает ее с суммарным спросом
        /// от работающих станков и выставляет каждому станку PowerEfficiency
        /// (от 0.0 до 1.0), которое влияет на скорость его работы.
        /// </summary>
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var stationStateLookup = SystemAPI.GetComponentLookup<StationState>();
            var recipeBlob = SystemAPI.GetSingleton<ProductionRecipeRegistryData>().Blob;
            if (!recipeBlob.IsCreated) return;
            ref var registry = ref recipeBlob.Value;

            var allocator = Allocator.Temp;

            foreach (var (usage, slots, workshopEntity) in SystemAPI.Query<RefRO<NetLinkUsage>, DynamicBuffer<StationSlot>>()
                .WithAll<WorkshopTag, ProductionActiveTag>().WithEntityAccess())
            {
                float availablePowerKW = usage.ValueRO.InUsedKW;
                float totalDemandKW = 0f;

                var demandingStations = new NativeList<Entity>(slots.Length, allocator);

                foreach (var slot in slots)
                {
                    if (!stationStateLookup.HasComponent(slot.Station)) continue;

                    var stationStateRW = stationStateLookup.GetRefRW(slot.Station);
                    stationStateRW.ValueRW.PowerEfficiency = 0f;

                    var stationState = stationStateRW.ValueRO;
                    if (stationState.Enabled == 0) continue;

                    var status = stationState.Status;
                    if (status == StationStatus.Working || status == StationStatus.ApplyingManualLabor)
                    {
                        ref var recipe = ref FindRecipe(ref registry, stationState.SelectedRecipeID);
                        if (recipe.RecipeID != -1)
                        {
                            totalDemandKW += recipe.RequiredKW;
                            demandingStations.Add(slot.Station);
                        }
                    }
                }

                if (demandingStations.IsEmpty)
                {
                    if (availablePowerKW > 0.01f)
                    {
                        foreach (var slot in slots)
                        {
                            if (stationStateLookup.HasComponent(slot.Station) && stationStateLookup.GetRefRO(slot.Station).ValueRO.Enabled == 1)
                            {
                                stationStateLookup.GetRefRW(slot.Station).ValueRW.PowerEfficiency = 1.0f;
                            }
                        }
                    }
                    continue;
                }

                float powerRatio = math.saturate(availablePowerKW / totalDemandKW);

                foreach (var stationEntity in demandingStations)
                {
                    var stationState = stationStateLookup.GetRefRW(stationEntity);
                    if (stationState.ValueRO.PowerEfficiency < 1.0f && powerRatio >= 1.0f)
                    {
                        if (stationState.ValueRO.PowerEfficiency > 0.001f)
                        {
                            stationState.ValueRW.TimePenalty += 2.0f;
                        }
                    }
                    stationState.ValueRW.PowerEfficiency = powerRatio;
                }
            }
        }

        /// <summary>
        /// Вспомогательный метод для поиска рецепта по его ID в реестре.
        /// </summary>
        private static ref ProductionRecipe FindRecipe(ref ProductionRecipeRegistryBlob registry, int recipeID)
        {
            for (int i = 0; i < registry.Recipes.Length; i++)
            {
                if (registry.Recipes[i].RecipeID == recipeID)
                {
                    return ref registry.Recipes[i];
                }
            }
            return ref registry.Recipes[0];
        }
    }
}