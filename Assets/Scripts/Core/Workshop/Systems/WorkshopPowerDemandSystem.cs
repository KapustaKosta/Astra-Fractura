using Energy.Core;
using Energy.Core.Systems;
using Game.Production;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace Game.Workshop
{
    /// <summary>
    /// Система, которая рассчитывает общую потребность в энергии для каждого цеха.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ToggleStationRequestSystem))]
    [UpdateAfter(typeof(SetStationRecipeRequestSystem))]
    [UpdateBefore(typeof(EnergyDispatchSystem))]
    public partial struct WorkshopPowerDemandSystem : ISystem
    {
        /// <summary>
        /// Вызывается при создании системы для установки необходимых зависимостей.
        /// </summary>
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ProductionRecipeRegistryData>();
        }

        /// <summary>
        /// Основной метод обновления. Для каждого цеха суммирует потребляемую мощность (RequiredKW)
        /// всех установленных станков с выбранными рецептами и записывает результат
        /// в компонент ConsumerLoad, который затем используется системой энергетики.
        /// </summary>
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