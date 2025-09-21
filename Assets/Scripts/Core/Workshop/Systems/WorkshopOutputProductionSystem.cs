using Game.Production;
using Unity.Burst;
using Unity.Entities;

namespace Game.Workshop
{
    /// <summary>
    /// Система, которая создает готовую продукцию на станке после завершения производственного цикла.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(WorkshopProgressSystem))]
    public partial struct WorkshopOutputProductionSystem : ISystem
    {
        /// <summary>
        /// Вызывается при создании системы для установки необходимых зависимостей.
        /// </summary>
        [BurstCompile]
        public void OnCreate(ref SystemState s)
        {
            s.RequireForUpdate<ProductionRecipeRegistryData>();
        }

        /// <summary>
        /// Основной метод обновления. Находит станки со статусом ProductionComplete,
        /// определяет по рецепту, какой продукт должен быть создан, и добавляет его
        /// в выходной буфер станка, после чего меняет статус на OutputPendingTransfer.
        /// </summary>
        [BurstCompile]
        public void OnUpdate(ref SystemState s)
        {
            var blob = SystemAPI.GetSingleton<ProductionRecipeRegistryData>().Blob;
            if (!blob.IsCreated) return;
            ref var registry = ref blob.Value;

            foreach (var (stationState, stationOutputBuffer, entity) in SystemAPI.Query<RefRW<StationState>, DynamicBuffer<StationOutputBufferElement>>().WithAll<StationTag>().WithEntityAccess())
            {
                if (stationState.ValueRO.Status != StationStatus.ProductionComplete) continue;

                int recipeIndex = FindRecipeIndex(ref registry, stationState.ValueRO.SelectedRecipeID);
                if (recipeIndex == -1) continue;
                ref var recipe = ref registry.Recipes[recipeIndex];

                stationOutputBuffer.Add(new StationOutputBufferElement { ItemID = recipe.OutputItemID, Amount = recipe.OutputAmount });

                stationState.ValueRW.AppliedHammerCost = 0;
                stationState.ValueRW.TimePenalty = 0;
                stationState.ValueRW.RemainingTime = 0f;
                stationState.ValueRW.Status = StationStatus.OutputPendingTransfer;
            }
        }
        
        /// <summary>
        /// Вспомогательный метод для поиска индекса рецепта по его ID.
        /// </summary>
        private int FindRecipeIndex(ref ProductionRecipeRegistryBlob registry, int recipeID)
        {
            for (int i = 0; i < registry.Recipes.Length; i++)
                if (registry.Recipes[i].RecipeID == recipeID) return i;
            return -1;
        }
    }
}