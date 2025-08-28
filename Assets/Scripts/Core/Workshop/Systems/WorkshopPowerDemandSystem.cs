using Energy.Core;
using Energy.Core.Systems;
using Game.Production;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace Game.Workshop
{
    /// <summary>
    /// Рассчитывает ПОТЕНЦИАЛЬНУЮ потребность цеха в энергии на основе УСТАНОВЛЕННЫХ
    /// станков и ВЫБРАННЫХ рецептов, НЕЗАВИСИМО от того, включены ли они.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(WorkshopControlSystem))]
    [UpdateBefore(typeof(EnergyDispatchSystem))]
    public partial struct WorkshopPowerDemandSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ProductionRecipeRegistryData>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var blob = SystemAPI.GetSingleton<ProductionRecipeRegistryData>().Blob;
            if (!blob.IsCreated) return;
            ref var registry = ref blob.Value;

            state.Dependency.Complete();

            var stationConfigLookup = SystemAPI.GetComponentLookup<StationConfig>(true);
            var stationStateLookup = SystemAPI.GetComponentLookup<StationState>(true);

            foreach (var (load, slots) in SystemAPI.Query<RefRW<ConsumerLoad>, DynamicBuffer<StationSlot>>().WithAll<WorkshopTag>())
            {
                float totalDemandKW = 0f;

                for (int i = 0; i < slots.Length; i++)
                {
                    var stationEntity = slots[i].Station;
                    if (!stationStateLookup.HasComponent(stationEntity) || !stationConfigLookup.HasComponent(stationEntity)) continue;

                    var stationState = stationStateLookup[stationEntity];
                    var stationConfig = stationConfigLookup[stationEntity];

                    // Суммируем энергию, если в слоте есть станок (TypeID != -1) и выбран рецепт.
                    // Флаг 'Enabled' полностью игнорируется для расчета ПОТЕНЦИАЛЬНОЙ нагрузки.
                    if (stationConfig.StationTypeID != -1 && stationState.SelectedRecipeID != -1)
                    {
                        ref var recipe = ref FindRecipe(ref registry, stationState.SelectedRecipeID);
                        if (recipe.RecipeID != -1)
                        {
                            totalDemandKW += recipe.RequiredKW;
                        }
                    }
                }

                load.ValueRW.CurrentKW = totalDemandKW;
            }
        }

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