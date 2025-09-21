using Game.Production;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Workshop
{
    /// <summary>
    /// Система, отвечающая за перемещение готовой продукции из выходного буфера станка
    /// либо во внутренний инвентарь (WIP) цеха (если продукт нужен следующему станку),
    /// либо в финальный выходной инвентарь цеха.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(WorkshopOutputProductionSystem))]
    public partial struct WorkshopOutputTransferSystem : ISystem
    {
        /// <summary>
        /// Компаратор для сортировки слотов станций по их порядку в цепочке.
        /// </summary>
        private struct StationSlotComparer : System.Collections.Generic.IComparer<StationSlot>
        {
            public int Compare(StationSlot x, StationSlot y) => x.Order.CompareTo(y.Order);
        }
        
        /// <summary>
        /// Вызывается при создании системы для установки необходимых зависимостей.
        /// </summary>
        [BurstCompile]
        public void OnCreate(ref SystemState s)
        {
            s.RequireForUpdate<ProductionRecipeRegistryData>();
        }

        /// <summary>
        /// Основной метод обновления. Итерирует по всем активным цехам, сортирует их станки
        /// и для каждого станка в статусе OutputPendingTransfer вызывает логику перемещения продукта.
        /// </summary>
        [BurstCompile]
        public void OnUpdate(ref SystemState s)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(s.WorldUnmanaged);
            var blob = SystemAPI.GetSingleton<ProductionRecipeRegistryData>().Blob;
            if (!blob.IsCreated) return;
            ref var registry = ref blob.Value;

            var stationStateLookup = SystemAPI.GetComponentLookup<StationState>(true);

            foreach (var (slots, workshopEntity) in SystemAPI.Query<DynamicBuffer<StationSlot>>().WithAll<WorkshopTag, ProductionActiveTag>().WithEntityAccess())
            {
                var sortedSlots = slots.ToNativeArray(Allocator.Temp);
                sortedSlots.Sort(new StationSlotComparer());

                for (int i = 0; i < sortedSlots.Length; i++)
                {
                    var stEnt = sortedSlots[i].Station;
                    if (SystemAPI.GetComponent<StationState>(stEnt).Status != StationStatus.OutputPendingTransfer) continue;

                    TransferProduct(stEnt, i, in sortedSlots, ref s, ref registry, ecb);
                }

                sortedSlots.Dispose();
            }
        }
        
        /// <summary>
        /// Перемещает продукт со станка, определяя, является ли он промежуточным или конечным.
        /// </summary>
        private void TransferProduct(Entity stationEntity, int stationIndex, in NativeArray<StationSlot> sortedSlots, ref SystemState s, ref ProductionRecipeRegistryBlob registry, EntityCommandBuffer ecb)
        {
            var stationOutput = SystemAPI.GetBuffer<StationOutputBufferElement>(stationEntity);
            if (stationOutput.IsEmpty) return;

            var workshopEntity = SystemAPI.GetComponent<StationOwner>(stationEntity).Workshop;
            int producedItemID = stationOutput[0].ItemID;
            int producedAmount = stationOutput[0].Amount;

            if (producedItemID == 0)
            {
                stationOutput.Clear();
                SystemAPI.GetComponentRW<StationState>(stationEntity).ValueRW.Status = StationStatus.Idle;
                return;
            }

            bool isNeededLater = IsItemRequiredBySubsequentActiveStations(producedItemID, stationIndex, in sortedSlots, ref s, ref registry);
            bool transferSuccess = false;

            if (isNeededLater)
            {
                var wipBuffer = SystemAPI.GetBuffer<WorkshopWIPBufferElement>(workshopEntity);
                int amountLeft = producedAmount;
                if (TryAddToWIPInventory(ref wipBuffer, producedItemID, ref amountLeft, 9999))
                {
                    transferSuccess = true;
                }
            }
            else
            {
                var outputBuffer = SystemAPI.GetBuffer<OutputInventorySlot>(workshopEntity);
                int amountLeft = producedAmount;
                if (TryAddToOutputInventory(ref outputBuffer, producedItemID, ref amountLeft, 9999))
                {
                    transferSuccess = true;
                    HandleFinalProduct(ref s, workshopEntity, producedItemID, producedAmount, ecb, ref registry);
                }
            }

            if (transferSuccess)
            {
                ecb.AddComponent<InventoryChangedTag>(workshopEntity);
                stationOutput.Clear();
                SystemAPI.GetComponentRW<StationState>(stationEntity).ValueRW.Status = StationStatus.Idle;
            }
        }
        
        /// <summary>
        /// Обрабатывает производство конечного продукта, обновляя очередь заказов цеха.
        /// </summary>
        private void HandleFinalProduct(ref SystemState s, Entity workshopEntity, int producedItemID, int producedAmount, EntityCommandBuffer ecb, ref ProductionRecipeRegistryBlob registry)
        {
            if (!SystemAPI.HasBuffer<WorkshopProductionQueueItem>(workshopEntity)) return;
            var queue = SystemAPI.GetBuffer<WorkshopProductionQueueItem>(workshopEntity);
            if (queue.IsEmpty) return;

            ref var task = ref queue.ElementAt(0);

            int finalRecipeIndex = FindRecipeIndex(ref registry, task.FinalRecipeID);
            if (finalRecipeIndex == -1) return;
            ref var finalRecipe = ref registry.Recipes[finalRecipeIndex];

            if (finalRecipe.OutputItemID == producedItemID)
            {
                if (task.AmountToProduce > 0)
                {
                    task.AmountToProduce -= producedAmount;
                }

                if (task.AmountToProduce <= 0 && task.InitialAmount != -1)
                {
                    queue.RemoveAt(0);
                    if (queue.IsEmpty)
                    {
                        ecb.AddComponent<RequestHaltProduction>(workshopEntity);
                    }
                }
            }
        }
        
        /// <summary>
        /// Проверяет, требуется ли произведенный предмет последующими станками в производственной цепочке.
        /// </summary>
        private bool IsItemRequiredBySubsequentActiveStations(int itemID, int currentStationIndex, in NativeArray<StationSlot> sortedSlots, ref SystemState s, ref ProductionRecipeRegistryBlob registry)
        {
            for (int i = currentStationIndex + 1; i < sortedSlots.Length; i++)
            {
                var nextStationState = SystemAPI.GetComponent<StationState>(sortedSlots[i].Station);
                if (nextStationState.Enabled == 1 && nextStationState.SelectedRecipeID != -1)
                {
                    int recipeIndex = FindRecipeIndex(ref registry, nextStationState.SelectedRecipeID);
                    if (recipeIndex == -1) continue;
                    ref var recipe = ref registry.Recipes[recipeIndex];

                    for (int j = 0; j < recipe.Inputs.Length; j++)
                    {
                        if (recipe.Inputs[j].ItemID == itemID) return true;
                    }
                }
            }
            return false;
        }
        
        /// <summary>
        /// Пытается добавить предметы в выходной инвентарь цеха.
        /// </summary>
        private bool TryAddToOutputInventory(ref DynamicBuffer<OutputInventorySlot> inventory, int itemID, ref int amount, int maxStack)
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
                    inventory[i] = new OutputInventorySlot { ItemID = itemID, Amount = toAdd };
                    amount -= toAdd;
                }
            }
            return amount < originalAmount;
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