using Unity.Entities;
using UnityEngine;

/// <summary>
/// Централизованная система для обработки запросов на изменение инвентарей (добавление/удаление предметов).
/// Работает с любыми сущностями, имеющими компонент HasInventoryTag.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(FinalizeBuildingSystem))] 
[UpdateAfter(typeof(ProcessHarvestRequestSystem))]
public partial class InventorySystem : SystemBase
{
    /// <summary>
    /// Вызывается каждый кадр для обработки запросов AddItemRequest и RemoveItemRequest.
    /// </summary>
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        
        var inventoryLookup = GetBufferLookup<InventoryItemElement>(false);
        var itemRegistry = ItemRegistry.Instance;

        if (itemRegistry == null) return;

        // Обработка запросов на добавление
        Entities
            .WithoutBurst()
            .ForEach((Entity requestEntity, in AddItemRequest request) =>
            {
                if (!inventoryLookup.HasBuffer(request.TargetInventoryOwner))
                {
                    ecb.DestroyEntity(requestEntity);
                    return;
                }

                var inventoryBuffer = inventoryLookup[request.TargetInventoryOwner];
                var itemData = itemRegistry.GetItemData(request.ItemID);
                
                if (itemData == null)
                {
                    ecb.DestroyEntity(requestEntity);
                    return;
                }
                
                int amountToAdd = request.Amount;

                // 1. Сначала пытаемся добавить в существующие неполные стаки
                if (itemData.maxStack > 1)
                {
                    for (int i = 0; i < inventoryBuffer.Length; i++)
                    {
                        if (amountToAdd <= 0) break;

                        var element = inventoryBuffer[i];
                        if (element.ItemID == request.ItemID && element.Amount < itemData.maxStack)
                        {
                            int spaceInStack = itemData.maxStack - element.Amount;
                            int amountToMove = Mathf.Min(amountToAdd, spaceInStack);
                            
                            element.Amount += amountToMove;
                            inventoryBuffer[i] = element;
                            amountToAdd -= amountToMove;
                        }
                    }
                }
                
                // 2. Если предметы еще остались, ищем пустые слоты и создаем новые стаки
                if (amountToAdd > 0)
                {
                    for (int i = 0; i < inventoryBuffer.Length; i++)
                    {
                        if (amountToAdd <= 0) break;

                        // Ищем пустой слот (по ItemID == 0)
                        if (inventoryBuffer[i].ItemID == 0)
                        {
                            int amountForNewStack = Mathf.Min(amountToAdd, itemData.maxStack);
                            inventoryBuffer[i] = new InventoryItemElement { ItemID = request.ItemID, Amount = amountForNewStack };
                            amountToAdd -= amountForNewStack;
                        }
                    }
                }

                ecb.DestroyEntity(requestEntity);
            }).Run();

        // Обработка запросов на удаление
        Entities.WithoutBurst().ForEach((Entity requestEntity, in RemoveItemRequest request) =>
        {
            if (inventoryLookup.HasBuffer(request.TargetInventoryOwner))
            {
                var inventoryBuffer = inventoryLookup[request.TargetInventoryOwner];
                int amountToRemove = request.Amount;

                // Итерируемся с конца, чтобы при удалении полных стаков это не влияло на следующие итерации
                for (int i = inventoryBuffer.Length - 1; i >= 0; i--)
                {
                    if (amountToRemove <= 0) break;

                    if (inventoryBuffer[i].ItemID == request.ItemID)
                    {
                        var element = inventoryBuffer[i];
                        int amountToTake = Mathf.Min(amountToRemove, element.Amount);
                        
                        element.Amount -= amountToTake;
                        amountToRemove -= amountToTake;

                        // Если стак опустел, мы не удаляем элемент, а "обнуляем" его
                        if (element.Amount <= 0)
                        {
                            inventoryBuffer[i] = new InventoryItemElement { ItemID = 0, Amount = 0 };
                        }
                        else
                        {
                            inventoryBuffer[i] = element;
                        }
                    }
                }
            }
            ecb.DestroyEntity(requestEntity);
        }).Run();
    }
}