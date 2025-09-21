using Unity.Burst;
using Unity.Entities;
using Game.Production;
using Unity.Mathematics;

namespace Game.Workshop
{
    /// <summary>
    /// Система, обрабатывающая запросы на включение или выключение отдельных станций в цехе.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(WorkshopActivationSystem))]
    public partial struct ToggleStationRequestSystem : ISystem
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
        /// Основной метод обновления. Обрабатывает запросы ToggleStationRequest,
        /// изменяет статус Enabled станции. При выключении работающей станции возвращает
        /// затраченные ресурсы в инвентарь цеха.
        /// </summary>
        [BurstCompile]
        public void OnUpdate(ref SystemState s)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(s.WorldUnmanaged);
            var blob = SystemAPI.GetSingleton<ProductionRecipeRegistryData>().Blob;
            if (!blob.IsCreated) return;
            ref var registry = ref blob.Value;

            foreach (var (req, ent) in SystemAPI.Query<RefRO<ToggleStationRequest>>().WithEntityAccess())
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

                    // Возврат ресурсов при выключении активной станции
                    if (!req.ValueRO.Enable && (st.Status == StationStatus.Working || st.Status == StationStatus.ApplyingManualLabor))
                    {
                        ref var recipe = ref FindRecipe(ref registry, st.SelectedRecipeID);
                        if (recipe.RecipeID != -1)
                        {
                            var wipBuffer = SystemAPI.GetBuffer<WorkshopWIPBufferElement>(req.ValueRO.Workshop);
                            for (int i = 0; i < recipe.Inputs.Length; i++)
                            {
                                int amountToReturn = recipe.Inputs[i].Amount;
                                TryAddToWIPInventory(ref wipBuffer, recipe.Inputs[i].ItemID, ref amountToReturn, 9999);
                            }
                        }
                    }

                    st.Enabled = (byte)(req.ValueRO.Enable ? 1 : 0);
                    st.PowerEfficiency = 0f;
                    st.PausedNoResources = 0;

                    if (st.Status != StationStatus.NeedsRepair && st.Status != StationStatus.Empty)
                    {
                        st.Status = req.ValueRO.Enable ? StationStatus.Idle : StationStatus.Offline;
                    }

                    SystemAPI.SetComponent(stEnt, st);
                }
                ecb.DestroyEntity(ent);
            }
        }

        /// <summary>
        /// Вспомогательный метод для поиска рецепта по его ID в реестре.
        /// </summary>
        private static ref ProductionRecipe FindRecipe(ref ProductionRecipeRegistryBlob registry, int recipeID)
        {
            for (int i = 0; i < registry.Recipes.Length; i++)
                if (registry.Recipes[i].RecipeID == recipeID) return ref registry.Recipes[i];
            return ref registry.Recipes[0];
        }

        /// <summary>
        /// Пытается добавить предметы во внутренний инвентарь (WIP) цеха.
        /// </summary>
        private bool TryAddToWIPInventory(ref DynamicBuffer<WorkshopWIPBufferElement> inventory, int itemID, ref int amount, int maxStack)
        {
            int originalAmount = amount;
            for (int i = 0; i < inventory.Length && amount > 0; i++)
            {
                var element = inventory[i];
                if (element.ItemID == itemID && element.Amount < maxStack)
                {
                    int space = maxStack - element.Amount;
                    int toAdd = math.min(amount, space);
                    element.Amount += toAdd;
                    inventory[i] = element;
                    amount -= toAdd;
                }
            }
            for (int i = 0; i < inventory.Length && amount > 0; i++)
            {
                if (inventory[i].ItemID == 0)
                {
                    int toAdd = math.min(amount, maxStack);
                    inventory[i] = new WorkshopWIPBufferElement { ItemID = itemID, Amount = toAdd };
                    amount -= toAdd;
                }
            }
            return amount < originalAmount;
        }
    }
}