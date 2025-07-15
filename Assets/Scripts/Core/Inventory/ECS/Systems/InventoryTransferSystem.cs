using Unity.Entities;
using UnityEngine;

/// <summary>
/// Централизованная система, отвечающая за обработку запросов на перенос
/// всех предметов из одного инвентаря в другой.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class InventoryTransferSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);
            
        var inventoryLookup = GetBufferLookup<InventoryItemElement>();
        var itemRegistry = ItemRegistry.Instance;
        if (itemRegistry == null) return;

        Entities
            .WithoutBurst()
            .WithAll<TransferItemsRequest>()
            .WithNativeDisableContainerSafetyRestriction(inventoryLookup)
            .ForEach((Entity requestEntity, in TransferItemsRequest request) =>
            {
                // Убеждаемся, что оба инвентаря существуют
                if (!inventoryLookup.HasBuffer(request.SourceOwner) || !inventoryLookup.HasBuffer(request.DestinationOwner))
                {
                    ecb.DestroyEntity(requestEntity);
                    return;
                }

                var sourceInventory = inventoryLookup[request.SourceOwner];
                var destinationInventory = inventoryLookup[request.DestinationOwner];

                // Начинаем операцию переноса предметов
                for (int i = 0; i < sourceInventory.Length; i++)
                {
                    var itemToTransfer = sourceInventory[i];
                    if (itemToTransfer.ItemID == 0 || itemToTransfer.Amount == 0) continue;

                    var itemData = itemRegistry.GetItemData(itemToTransfer.ItemID);
                    if (itemData == null) continue;

                    int amountLeftToTransfer = itemToTransfer.Amount;

                    // 1. Пытаемся добавить в существующие стаки в инвентаре-получателе.
                    for (int j = 0; j < destinationInventory.Length && amountLeftToTransfer > 0; j++)
                    {
                        var targetSlot = destinationInventory[j];
                        if (targetSlot.ItemID == itemToTransfer.ItemID && targetSlot.Amount < itemData.maxStack)
                        {
                            int spaceAvailable = itemData.maxStack - targetSlot.Amount;
                            int amount = Mathf.Min(amountLeftToTransfer, spaceAvailable);

                            targetSlot.Amount += amount;
                            destinationInventory[j] = targetSlot;
                            amountLeftToTransfer -= amount;
                        }
                    }

                    // 2. Пытаемся добавить в пустые слоты.
                    for (int j = 0; j < destinationInventory.Length && amountLeftToTransfer > 0; j++)
                    {
                        if (destinationInventory[j].ItemID == 0)
                        {
                            int amount = Mathf.Min(amountLeftToTransfer, itemData.maxStack);
                            destinationInventory[j] = new InventoryItemElement
                                { ItemID = itemToTransfer.ItemID, Amount = amount };
                            amountLeftToTransfer -= amount;
                        }
                    }
                    
                    // 3. Обновляем инвентарь источника.
                    int amountTransferred = itemToTransfer.Amount - amountLeftToTransfer;
                    if (amountTransferred > 0)
                    {
                        itemToTransfer.Amount -= amountTransferred;
                        sourceInventory[i] = itemToTransfer.Amount > 0 ? itemToTransfer : default;
                    }
                }

                // Проверяем, остались ли у источника предметы.
                bool hasItemsLeft = false;
                foreach (var item in sourceInventory)
                {
                    if (item.ItemID != 0 && item.Amount > 0)
                    {
                        hasItemsLeft = true;
                        break;
                    }
                }

                // Добавляем тег с результатом операции для других систем.
                if (hasItemsLeft)
                {
                    ecb.AddComponent<TransferFailedTag>(request.SourceOwner);
                }
                else
                {
                    ecb.AddComponent<TransferSuccessTag>(request.SourceOwner);
                }

                ecb.DestroyEntity(requestEntity);
            }).Run();
    }
}