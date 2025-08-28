using Unity.Entities;
using Game.Production;
using UnityEngine;

namespace Conveyor
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(RouteTransferSystem))]
    public partial class ItemTransitSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
            float currentTime = (float)SystemAPI.Time.ElapsedTime;

            var itemRegistry = ItemRegistry.Instance;
            if (itemRegistry == null) return;

            var inputInvLookup = GetBufferLookup<InputInventorySlot>(false);
            var generalInvLookup = GetBufferLookup<InventoryItemElement>(false);

            Entities
                .ForEach((Entity entity, in ItemInTransit item) =>
                {
                    float arrivalTime = item.StartTime + item.TravelDuration;
                    if (currentTime >= arrivalTime)
                    {
                        bool itemDelivered = false;

                        if (inputInvLookup.HasBuffer(item.DestinationInventory))
                        {
                            var buffer = inputInvLookup[item.DestinationInventory].Reinterpret<InventoryItemElement>();
                            if (TryAddItemToBuffer(buffer, item.ItemID, item.Amount, itemRegistry))
                            {
                                itemDelivered = true;
                                ecb.AddComponent<InventoryChangedTag>(item.DestinationInventory);
                            }
                        }

                        if (!itemDelivered && generalInvLookup.HasBuffer(item.DestinationInventory))
                        {
                            var buffer = generalInvLookup[item.DestinationInventory];
                            if (TryAddItemToBuffer(buffer, item.ItemID, item.Amount, itemRegistry))
                            {
                                itemDelivered = true;
                                ecb.AddComponent<InventoryChangedTag>(item.DestinationInventory);
                            }
                        }

                        if (itemDelivered)
                        {
                            ecb.DestroyEntity(entity);
                        }
                    }
                }).WithoutBurst().Run();
        }

        private bool TryAddItemToBuffer(DynamicBuffer<InventoryItemElement> buffer, int itemID, int amount, ItemRegistry registry)
        {
            var itemData = registry.GetItemData(itemID);
            if (itemData == null) return false;

            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].ItemID == itemID && buffer[i].Amount < itemData.maxStack)
                {
                    var slot = buffer[i];
                    int spaceAvailable = itemData.maxStack - slot.Amount;
                    int amountToAdd = Mathf.Min(amount, spaceAvailable);

                    slot.Amount += amountToAdd;
                    buffer[i] = slot;
                    return true;
                }
            }

            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].ItemID == 0)
                {
                    buffer[i] = new InventoryItemElement { ItemID = itemID, Amount = amount };
                    return true;
                }
            }

            return false;
        }
    }
}