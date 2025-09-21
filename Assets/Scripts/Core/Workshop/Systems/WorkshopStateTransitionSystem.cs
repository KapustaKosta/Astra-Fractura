using Game.Production;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Game.Workshop
{
    /// <summary>
    /// Система, управляющая переходами состояний (статусов) для станков внутри активного цеха.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(WorkshopActivationSystem))]
    public partial struct WorkshopStateTransitionSystem : ISystem
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
        /// Основной метод обновления. Для каждого активного цеха анализирует состояние
        /// его станков и наличие ресурсов. Переводит станки из ожидания в готовность
        /// к работе, если ресурсы есть, или из рабочего состояния в завершенное,
        /// когда таймер истек.
        /// </summary>
        [BurstCompile]
        public void OnUpdate(ref SystemState s)
        {
            var blob = SystemAPI.GetSingleton<ProductionRecipeRegistryData>().Blob;
            if (!blob.IsCreated) return;
            ref var registry = ref blob.Value;

            var inputLookup = SystemAPI.GetBufferLookup<InputInventorySlot>(true);
            var wipLookup = SystemAPI.GetBufferLookup<WorkshopWIPBufferElement>(true);
            var stationStateLookup = SystemAPI.GetComponentLookup<StationState>();

            foreach (var (slots, workshopEntity) in SystemAPI.Query<DynamicBuffer<StationSlot>>()
                .WithAll<WorkshopTag, ProductionActiveTag>()
                .WithEntityAccess())
            {
                if (slots.IsEmpty) continue;
                
                var virtualInventory = new NativeHashMap<int, int>(64, Allocator.Temp);
                if (inputLookup.HasBuffer(workshopEntity))
                    foreach (var item in inputLookup[workshopEntity])
                        if (item.ItemID != 0) virtualInventory.Increment(item.ItemID, item.Amount);

                if (wipLookup.HasBuffer(workshopEntity))
                    foreach (var item in wipLookup[workshopEntity])
                        if (item.ItemID != 0) virtualInventory.Increment(item.ItemID, item.Amount);
                
                foreach (var slot in slots)
                {
                    if (!stationStateLookup.HasComponent(slot.Station)) continue;

                    var st = stationStateLookup.GetRefRW(slot.Station);
                    if (st.ValueRO.Enabled == 0 || st.ValueRO.SelectedRecipeID == -1) continue;

                    int recipeIndex = FindRecipeIndex(ref registry, st.ValueRO.SelectedRecipeID);
                    if (recipeIndex == -1) continue;
                    ref var recipe = ref registry.Recipes[recipeIndex];

                    switch (st.ValueRO.Status)
                    {
                        case StationStatus.Idle:
                        case StationStatus.Offline:
                        case StationStatus.WaitingForInput:
                            if (HasInputs(in virtualInventory, ref recipe))
                            {
                                st.ValueRW.Status = StationStatus.AwaitingConsumption;
                            }
                            else
                            {
                                st.ValueRW.Status = StationStatus.WaitingForInput;
                            }
                            break;

                        case StationStatus.Working:
                            if (st.ValueRO.RemainingTime <= 0f)
                            {
                                st.ValueRW.Status = StationStatus.ProductionComplete;
                            }
                            break;
                    }
                }

                virtualInventory.Dispose();
            }
        }

        /// <summary>
        /// Проверяет, достаточно ли ресурсов в "виртуальном" инвентаре для запуска рецепта.
        /// </summary>
        private bool HasInputs(in NativeHashMap<int, int> virtualInventory, ref ProductionRecipe recipe)
        {
            if (recipe.Inputs.Length == 0) return true;
            for (int i = 0; i < recipe.Inputs.Length; i++)
            {
                var input = recipe.Inputs[i];
                if (!virtualInventory.TryGetValue(input.ItemID, out int available) || available < input.Amount)
                {
                    return false;
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