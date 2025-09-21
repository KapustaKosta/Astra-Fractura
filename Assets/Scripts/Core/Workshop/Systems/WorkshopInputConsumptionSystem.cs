using Game.Production;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Workshop
{
    /// <summary>
    /// Система, отвечающая за потребление входных ресурсов станками, готовыми начать производственный цикл.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(WorkshopStateTransitionSystem))]
    public partial struct WorkshopInputConsumptionSystem : ISystem
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
        /// Основной метод обновления. Находит станки в статусе AwaitingConsumption,
        /// проверяет, что их цех активен, и пытается списать необходимые по рецепту
        /// ресурсы из инвентарей цеха. При успехе переводит станок в рабочий статус.
        /// </summary>
        [BurstCompile]
        public void OnUpdate(ref SystemState s)
        {
            var blob = SystemAPI.GetSingleton<ProductionRecipeRegistryData>().Blob;
            if (!blob.IsCreated) return;
            ref var registry = ref blob.Value;
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(s.WorldUnmanaged);

            foreach (var (stationOwner, stationState) in SystemAPI.Query<RefRO<StationOwner>, RefRW<StationState>>()
                .WithAll<StationTag>()
                .WithNone<WorkshopUnderMaintenanceTag>())
            {
                if (stationState.ValueRO.Status != StationStatus.AwaitingConsumption) continue;

                var workshopEntity = stationOwner.ValueRO.Workshop;

                if (SystemAPI.HasComponent<Disabled>(workshopEntity) || !SystemAPI.HasComponent<ProductionActiveTag>(workshopEntity)) continue;

                int recipeIndex = FindRecipeIndex(ref registry, stationState.ValueRO.SelectedRecipeID);
                if (recipeIndex == -1) continue;
                ref var recipe = ref registry.Recipes[recipeIndex];

                if (ConsumeInputs(ref s, workshopEntity, ref recipe))
                {
                    ecb.AddComponent<InventoryChangedTag>(workshopEntity);
                    stationState.ValueRW.Status = recipe.HammerCost > 0 ? StationStatus.AwaitingManualLabor : StationStatus.Working;
                    if (recipe.HammerCost <= 0)
                        stationState.ValueRW.RemainingTime = recipe.BaseTime + stationState.ValueRO.TimePenalty;
                }
                else
                {
                    stationState.ValueRW.Status = StationStatus.WaitingForInput;
                }
            }
        }

        /// <summary>
        /// Списывает ресурсы, необходимые для рецепта, из входного и внутреннего инвентарей цеха.
        /// </summary>
        private bool ConsumeInputs(ref SystemState s, Entity workshop, ref ProductionRecipe recipe)
        {
            if (!SystemAPI.HasBuffer<InputInventorySlot>(workshop) || !SystemAPI.HasBuffer<WorkshopWIPBufferElement>(workshop)) return false;

            var inputBuffer = SystemAPI.GetBuffer<InputInventorySlot>(workshop);
            var wipBuffer = SystemAPI.GetBuffer<WorkshopWIPBufferElement>(workshop);

            for (int k = 0; k < recipe.Inputs.Length; k++)
            {
                var input = recipe.Inputs[k];
                int amountToConsume = input.Amount;

                for (int i = wipBuffer.Length - 1; i >= 0 && amountToConsume > 0; i--)
                {
                    if (wipBuffer[i].ItemID == input.ItemID)
                    {
                        int take = math.min(amountToConsume, wipBuffer[i].Amount);
                        var item = wipBuffer[i];
                        item.Amount -= take;
                        wipBuffer[i] = item.Amount > 0 ? item : default;
                        amountToConsume -= take;
                    }
                }
                for (int i = inputBuffer.Length - 1; i >= 0 && amountToConsume > 0; i--)
                {
                    if (inputBuffer[i].ItemID == input.ItemID)
                    {
                        int take = math.min(amountToConsume, inputBuffer[i].Amount);
                        var item = inputBuffer[i];
                        item.Amount -= take;
                        inputBuffer[i] = item.Amount > 0 ? item : default;
                        amountToConsume -= take;
                    }
                }
            }
            return true;
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