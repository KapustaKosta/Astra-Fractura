using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Game.Production;

namespace Game.Workshop
{
    /// <summary>
    /// Система, обрабатывающая запросы на удаление (очистку) станции из слота цеха.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(WorkshopActivationSystem))]
    public partial struct RemoveStationRequestSystem : ISystem
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
        /// Основной метод обновления. Обрабатывает все запросы RemoveStationRequest,
        /// возвращает ресурсы, если станция работала, перемещает готовую продукцию
        /// на склад цеха и сбрасывает состояние станции перед удалением запроса.
        /// </summary>
        [BurstCompile]
        public void OnUpdate(ref SystemState s)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(s.WorldUnmanaged);
            var blob = SystemAPI.GetSingleton<ProductionRecipeRegistryData>().Blob;
            if (!blob.IsCreated) return;
            ref var registry = ref blob.Value;

            foreach (var (req, ent) in SystemAPI.Query<RefRO<RemoveStationRequest>>().WithEntityAccess())
            {
                if (!SystemAPI.Exists(req.ValueRO.Workshop) || !SystemAPI.HasBuffer<StationSlot>(req.ValueRO.Workshop))
                { ecb.DestroyEntity(ent); continue; }

                ecb.AddComponent<WorkshopChainChangedTag>(req.ValueRO.Workshop);

                var slots = SystemAPI.GetBuffer<StationSlot>(req.ValueRO.Workshop);
                if (req.ValueRO.SlotIndex < 0 || req.ValueRO.SlotIndex >= slots.Length) { ecb.DestroyEntity(ent); continue; }

                var stEnt = slots[req.ValueRO.SlotIndex].Station;
                if (SystemAPI.Exists(stEnt))
                {
                    // Возврат ресурсов, если станок был в процессе работы
                    if (SystemAPI.HasComponent<StationState>(stEnt))
                    {
                        var st = SystemAPI.GetComponent<StationState>(stEnt);
                        if (st.Status == StationStatus.Working || st.Status == StationStatus.ApplyingManualLabor)
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
                    }

                    // Перемещение готовой продукции со станка на выходной склад цеха
                    if (SystemAPI.HasBuffer<StationOutputBufferElement>(stEnt) && SystemAPI.HasBuffer<OutputInventorySlot>(req.ValueRO.Workshop))
                    {
                        var stationOutput = SystemAPI.GetBuffer<StationOutputBufferElement>(stEnt);
                        var workshopOutputBuffer = SystemAPI.GetBuffer<OutputInventorySlot>(req.ValueRO.Workshop);

                        if (!stationOutput.IsEmpty)
                        {
                            var itemsToMove = stationOutput.ToNativeArray(Allocator.Temp);
                            stationOutput.Clear();
                            var workshopOutputItems = workshopOutputBuffer.Reinterpret<InventoryItemElement>();
                            for (int i = 0; i < itemsToMove.Length; i++)
                            {
                                var item = itemsToMove[i];
                                if (item.ItemID == 0 || item.Amount == 0) continue;
                                int amountLeft = item.Amount;
                                TryAddToOutputInventory(ref workshopOutputItems, item.ItemID, ref amountLeft, 1000);
                            }
                            itemsToMove.Dispose();
                        }
                    }
                    
                    // Сброс состояния и конфигурации станка
                    if (SystemAPI.HasComponent<StationState>(stEnt))
                    {
                        var st = new StationState { Status = StationStatus.Empty, SelectedRecipeID = -1, };
                        SystemAPI.SetComponent(stEnt, st);
                    }

                    if (SystemAPI.HasComponent<StationConfig>(stEnt))
                    {
                        var cfg = SystemAPI.GetComponent<StationConfig>(stEnt);
                        cfg.StationTypeID = -1;
                        SystemAPI.SetComponent(stEnt, cfg);
                    }
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

        /// <summary>
        /// Пытается добавить предметы в выходной инвентарь цеха.
        /// </summary>
        private bool TryAddToOutputInventory(ref DynamicBuffer<InventoryItemElement> inventory, int itemID, ref int amount, int maxStack)
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
                    inventory[i] = new InventoryItemElement { ItemID = itemID, Amount = toAdd };
                    amount -= toAdd;
                }
            }
            return amount < originalAmount;
        }
    }
}