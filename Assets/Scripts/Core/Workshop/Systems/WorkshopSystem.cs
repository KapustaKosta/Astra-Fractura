using Game.Production;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Workshop
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(WorkshopLifecycleSystem))]
    public partial struct WorkshopSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState s)
        {
            s.RequireForUpdate<ProductionRecipeRegistryData>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState s)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var blob = SystemAPI.GetSingleton<ProductionRecipeRegistryData>().Blob;
            if (!blob.IsCreated) return;
            ref var registry = ref blob.Value;

            var inputLookup = SystemAPI.GetBufferLookup<InputInventorySlot>(true);
            var wipLookup = SystemAPI.GetBufferLookup<WorkshopWIPBufferElement>(true);
            var stationStateLookup = SystemAPI.GetComponentLookup<StationState>(false);
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(s.WorldUnmanaged);

            s.Dependency.Complete();

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

                var sortedSlots = slots.ToNativeArray(Allocator.Temp);
                sortedSlots.Sort(new StationSlotComparer());

                for (int i = 0; i < sortedSlots.Length; i++)
                {
                    var stEnt = sortedSlots[i].Station;
                    if (!stationStateLookup.HasComponent(stEnt)) continue;

                    var st = stationStateLookup.GetRefRW(stEnt);
                    if (st.ValueRO.Enabled == 0 || st.ValueRO.SelectedRecipeID == -1) continue;

                    int recipeIndex = FindRecipeIndex(ref registry, st.ValueRO.SelectedRecipeID);
                    if (recipeIndex == -1) continue;
                    ref var recipe = ref registry.Recipes[recipeIndex];

                    switch (st.ValueRO.Status)
                    {
                        case StationStatus.Idle:
                        case StationStatus.Offline:
                            if (HasInputs(in virtualInventory, ref recipe))
                            {
                                if (ConsumeInputs(ref s, workshopEntity, ref virtualInventory, ref recipe))
                                {

                                    ecb.AddComponent<InventoryChangedTag>(workshopEntity); // Уведомляем систему об изменении инвентаря

                                    st.ValueRW.Status = recipe.HammerCost > 0 ? StationStatus.AwaitingManualLabor : StationStatus.Working;
                                    if (recipe.HammerCost <= 0)
                                        st.ValueRW.RemainingTime = recipe.BaseTime + st.ValueRO.TimePenalty;
                                }
                            }
                            else
                            {
                                st.ValueRW.Status = StationStatus.WaitingForInput;
                            }
                            break;

                        case StationStatus.Working:
                            st.ValueRW.RemainingTime -= dt;
                            if (st.ValueRO.RemainingTime <= 0f)
                            {
                                var stationOutput = SystemAPI.GetBuffer<StationOutputBufferElement>(stEnt);
                                stationOutput.Add(new StationOutputBufferElement { ItemID = recipe.OutputItemID, Amount = recipe.OutputAmount });

                                TransferProductAndRefreshVirtualInv(stEnt, i, in sortedSlots, ref s, ref registry, ref virtualInventory, ecb);

                                st.ValueRW.AppliedHammerCost = 0;
                                st.ValueRW.TimePenalty = 0;
                                st.ValueRW.RemainingTime = 0f;
                                st.ValueRW.Status = StationStatus.Idle;
                            }
                            break;

                        case StationStatus.WaitingForInput:
                            if (HasInputs(in virtualInventory, ref recipe))
                            {
                                st.ValueRW.Status = StationStatus.Idle;
                            }
                            break;
                    }
                }
                virtualInventory.Dispose();
                sortedSlots.Dispose();
            }
        }

        private void TransferProductAndRefreshVirtualInv(Entity stationEntity, int stationIndex, in NativeArray<StationSlot> sortedSlots, ref SystemState s, ref ProductionRecipeRegistryBlob registry, ref NativeHashMap<int, int> virtualInventory, EntityCommandBuffer ecb)
        {
            var stationOutput = SystemAPI.GetBuffer<StationOutputBufferElement>(stationEntity);
            if (stationOutput.IsEmpty) return;

            var workshopEntity = SystemAPI.GetComponent<StationOwner>(stationEntity).Workshop;
            int producedItemID = stationOutput[0].ItemID;
            int producedAmount = stationOutput[0].Amount;

            if (producedItemID == 0)
            {
                stationOutput.Clear();
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
                    virtualInventory.Increment(producedItemID, producedAmount - amountLeft);
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

                ecb.AddComponent<InventoryChangedTag>(workshopEntity); // Уведомляем систему об изменении инвентаря

                stationOutput.Clear();
            }
        }

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

        private bool ConsumeInputs(ref SystemState s, Entity workshop, ref NativeHashMap<int, int> virtualInventory, ref ProductionRecipe recipe)
        {
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
                virtualInventory.Decrement(input.ItemID, input.Amount);
            }
            return true;
        }

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

        private int FindRecipeIndex(ref ProductionRecipeRegistryBlob registry, int recipeID)
        {
            for (int i = 0; i < registry.Recipes.Length; i++)
                if (registry.Recipes[i].RecipeID == recipeID) return i;
            return -1;
        }

        private struct StationSlotComparer : System.Collections.Generic.IComparer<StationSlot>
        {
            public int Compare(StationSlot x, StationSlot y) => x.Order.CompareTo(y.Order);
        }
    }
}