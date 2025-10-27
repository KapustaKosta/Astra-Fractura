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
            float deltaTime = SystemAPI.Time.DeltaTime;

            var itemRegistry = ItemRegistry.Instance;
            if (itemRegistry == null) return;

            var inputInvLookup = GetBufferLookup<InputInventorySlot>(false);
            var generalInvLookup = GetBufferLookup<InventoryItemElement>(false);
            var powerScalingLookup = GetComponentLookup<RoutePowerScaling>(true);

            Entities
                .WithReadOnly(powerScalingLookup)
                .ForEach((Entity entity, ref ItemInTransit item) =>
                {
                    float speedMultiplier = powerScalingLookup.TryGetComponent(item.RouteEntity, out var scaling)
                        ? scaling.SpeedMultiplier
                        : 0.0f; 

                    if (speedMultiplier > 0.001f)
                    {
                        item.CurrentTravelTime += deltaTime; // TravelTime is now independent of speed, speed affects visual only
                    }

                    if (item.CurrentTravelTime >= item.TravelDuration)
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