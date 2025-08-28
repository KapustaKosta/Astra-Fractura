using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Game.Production;
using Unity.Collections;

namespace Game.Workshop
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(WorkshopSystem))]
    public partial struct WorkshopControlSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState s)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                               .CreateCommandBuffer(s.WorldUnmanaged);

            // Вкл/выкл целого цеха
            foreach (var (req, ent) in SystemAPI.Query<RefRO<ToggleWorkshopRequest>>().WithEntityAccess())
            {
                if (SystemAPI.Exists(req.ValueRO.Workshop) && SystemAPI.HasComponent<WorkshopState>(req.ValueRO.Workshop))
                {
                    var st = SystemAPI.GetComponent<WorkshopState>(req.ValueRO.Workshop);
                    st.IsOn = req.ValueRO.Enable;
                    SystemAPI.SetComponent(req.ValueRO.Workshop, st);
                    ecb.AddComponent<WorkshopChainChangedTag>(req.ValueRO.Workshop); // Добавляем тег
                }
                ecb.DestroyEntity(ent);
            }

            // Вкл/выкл станции
            foreach (var (req, ent) in SystemAPI.Query<RefRO<ToggleStationRequest>>().WithEntityAccess())
            {
                if (!SystemAPI.Exists(req.ValueRO.Workshop) || !SystemAPI.HasBuffer<StationSlot>(req.ValueRO.Workshop))
                { ecb.DestroyEntity(ent); continue; }

                ecb.AddComponent<WorkshopChainChangedTag>(req.ValueRO.Workshop); // Добавляем тег

                var slots = SystemAPI.GetBuffer<StationSlot>(req.ValueRO.Workshop);
                if (req.ValueRO.SlotIndex < 0 || req.ValueRO.SlotIndex >= slots.Length) { ecb.DestroyEntity(ent); continue; }

                var stEnt = slots[req.ValueRO.SlotIndex].Station;
                if (SystemAPI.Exists(stEnt) && SystemAPI.HasComponent<StationState>(stEnt))
                {
                    var st = SystemAPI.GetComponent<StationState>(stEnt);
                    st.Enabled = (byte)(req.ValueRO.Enable ? 1 : 0);
                    st.PausedNoPower = 0;
                    st.PausedNoResources = 0;

                    if (st.Status != StationStatus.NeedsRepair && st.Status != StationStatus.Empty)
                    {
                        st.Status = req.ValueRO.Enable ? StationStatus.Idle : StationStatus.Offline;
                    }

                    SystemAPI.SetComponent(stEnt, st);
                }
                ecb.DestroyEntity(ent);
            }

            // Удалить станцию
            foreach (var (req, ent) in SystemAPI.Query<RefRO<RemoveStationRequest>>().WithEntityAccess())
            {
                if (!SystemAPI.Exists(req.ValueRO.Workshop) || !SystemAPI.HasBuffer<StationSlot>(req.ValueRO.Workshop))
                { ecb.DestroyEntity(ent); continue; }

                ecb.AddComponent<WorkshopChainChangedTag>(req.ValueRO.Workshop); // Добавляем тег

                var slots = SystemAPI.GetBuffer<StationSlot>(req.ValueRO.Workshop);
                if (req.ValueRO.SlotIndex < 0 || req.ValueRO.SlotIndex >= slots.Length) { ecb.DestroyEntity(ent); continue; }

                var stEnt = slots[req.ValueRO.SlotIndex].Station;
                if (SystemAPI.Exists(stEnt))
                {
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
                                TryAddToInventory(ref workshopOutputItems, item.ItemID, ref amountLeft, 1000);
                            }
                            itemsToMove.Dispose();
                        }
                    }

                    if (SystemAPI.HasComponent<StationState>(stEnt))
                    {
                        var st = new StationState
                        {
                            Status = StationStatus.Empty,
                            SelectedRecipeID = -1,
                        };
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

            // Смена рецепта
            foreach (var (req, ent) in SystemAPI.Query<RefRO<SetStationRecipeRequest>>().WithEntityAccess())
            {
                if (!SystemAPI.Exists(req.ValueRO.Workshop) || !SystemAPI.HasBuffer<StationSlot>(req.ValueRO.Workshop))
                { ecb.DestroyEntity(ent); continue; }

                ecb.AddComponent<WorkshopChainChangedTag>(req.ValueRO.Workshop); // Добавляем тег

                var slots = SystemAPI.GetBuffer<StationSlot>(req.ValueRO.Workshop);
                if (req.ValueRO.SlotIndex < 0 || req.ValueRO.SlotIndex >= slots.Length) { ecb.DestroyEntity(ent); continue; }

                var stEnt = slots[req.ValueRO.SlotIndex].Station;
                if (SystemAPI.Exists(stEnt) && SystemAPI.HasComponent<StationState>(stEnt))
                {
                    var st = SystemAPI.GetComponent<StationState>(stEnt);
                    if (st.SelectedRecipeID != req.ValueRO.RecipeID)
                    {
                        st.SelectedRecipeID = req.ValueRO.RecipeID;
                        st.RemainingTime = 0;
                        st.AppliedHammerCost = 0;
                        st.TimePenalty = 0;

                        if (st.Status != StationStatus.Empty && st.Status != StationStatus.NeedsRepair)
                        {
                            st.Status = StationStatus.Idle;
                        }
                    }
                    SystemAPI.SetComponent(stEnt, st);
                }
                ecb.DestroyEntity(ent);
            }

            // Поменять порядок слотов (swap)
            foreach (var (req, ent) in SystemAPI.Query<RefRO<MoveStationRequest>>().WithEntityAccess())
            {
                if (!SystemAPI.Exists(req.ValueRO.Workshop) || !SystemAPI.HasBuffer<StationSlot>(req.ValueRO.Workshop))
                { ecb.DestroyEntity(ent); continue; }

                ecb.AddComponent<WorkshopChainChangedTag>(req.ValueRO.Workshop); // Добавляем тег

                var slots = SystemAPI.GetBuffer<StationSlot>(req.ValueRO.Workshop);
                int a = req.ValueRO.FromIndex, b = req.ValueRO.ToIndex;
                if (a < 0 || b < 0 || a >= slots.Length || b >= slots.Length || a == b) { ecb.DestroyEntity(ent); continue; }

                var tempA = slots[a];
                slots[a] = slots[b];
                slots[b] = tempA;

                ecb.DestroyEntity(ent);
            }
        }

        private bool TryAddToInventory(ref DynamicBuffer<InventoryItemElement> inventory, int itemID, ref int amount, int maxStack)
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