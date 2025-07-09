using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Система, обрабатывающая запросы на разделение стаков (SplitStackRequest).
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(MoveItemSystem))]
public partial class SplitStackSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        var bufferLookup = GetBufferLookup<InventoryItemElement>(false);
        var itemRegistry = ItemRegistry.Instance;

        if (itemRegistry == null) return;

        Entities
            .WithoutBurst()
            .ForEach((Entity requestEntity, in SplitStackRequest request) =>
            {
                if (!bufferLookup.HasBuffer(request.SourceInventoryOwner) ||
                    !bufferLookup.HasBuffer(request.DestinationInventoryOwner) ||
                    request.AmountToMove <= 0)
                {
                    ecb.DestroyEntity(requestEntity);
                    return;
                }

                var sourceBuffer = bufferLookup[request.SourceInventoryOwner];
                var destBuffer = bufferLookup[request.DestinationInventoryOwner];

                if (request.SourceSlotIndex >= sourceBuffer.Length || request.DestinationSlotIndex >= destBuffer.Length)
                {
                    ecb.DestroyEntity(requestEntity);
                    return;
                }

                var sourceItem = sourceBuffer[request.SourceSlotIndex];
                var destItem = destBuffer[request.DestinationSlotIndex];
                
                if (sourceItem.Amount < request.AmountToMove)
                {
                    ecb.DestroyEntity(requestEntity);
                    return;
                }
                
                var itemData = itemRegistry.GetItemData(sourceItem.ItemID);
                if (itemData == null)
                {
                    ecb.DestroyEntity(requestEntity);
                    return;
                }

                if (destItem.ItemID == 0)
                {
                    sourceItem.Amount -= request.AmountToMove;
                    if (sourceItem.Amount <= 0)
                        sourceBuffer[request.SourceSlotIndex] = new InventoryItemElement();
                    else
                        sourceBuffer[request.SourceSlotIndex] = sourceItem;

                    destBuffer[request.DestinationSlotIndex] = new InventoryItemElement
                    {
                        ItemID = sourceItem.ItemID,
                        Amount = request.AmountToMove
                    };
                }
                else if (destItem.ItemID == sourceItem.ItemID && destItem.Amount < itemData.maxStack)
                {
                    int spaceAvailable = itemData.maxStack - destItem.Amount;
                    int amountToActuallyMove = math.min(request.AmountToMove, spaceAvailable);

                    sourceItem.Amount -= amountToActuallyMove;
                     if (sourceItem.Amount <= 0)
                        sourceBuffer[request.SourceSlotIndex] = new InventoryItemElement();
                    else
                        sourceBuffer[request.SourceSlotIndex] = sourceItem;

                    destItem.Amount += amountToActuallyMove;
                    destBuffer[request.DestinationSlotIndex] = destItem;
                }

                ecb.DestroyEntity(requestEntity);

            }).Run();
    }
}