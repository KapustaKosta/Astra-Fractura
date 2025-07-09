using Unity.Collections;
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
        var propertiesLookup = GetComponentLookup<InventoryProperties>(true);
        var itemRegistry = ItemRegistry.Instance;

        // Если реестр предметов еще не готов, прерываем выполнение, чтобы избежать ошибок.
        if (itemRegistry == null)
        {
            // Уничтожаем все необработанные запросы, чтобы они не накапливались.
            var addRequestQuery = SystemAPI.QueryBuilder().WithAll<AddItemRequest>().Build();
            var removeRequestQuery = SystemAPI.QueryBuilder().WithAll<RemoveItemRequest>().Build();
            if (!addRequestQuery.IsEmpty) ecb.DestroyEntity(addRequestQuery, EntityQueryCaptureMode.AtPlayback);
            if (!removeRequestQuery.IsEmpty) ecb.DestroyEntity(removeRequestQuery, EntityQueryCaptureMode.AtPlayback);
            return;
        }

        // Обработка запросов на добавление 
        Entities
            .WithoutBurst() // т.к. мы обращаемся к управляемому объекту itemRegistry
            .ForEach((Entity requestEntity, in AddItemRequest request) =>
        {
            if (inventoryLookup.HasBuffer(request.TargetInventoryOwner) && propertiesLookup.HasComponent(request.TargetInventoryOwner))
            {
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
                            inventoryBuffer[i] = element; // Записываем измененную структуру обратно
                            amountToAdd -= amountToMove;
                        }
                    }
                }
                
                // 2. Если предметы еще остались, создаем новые стаки в свободных слотах
                if (amountToAdd > 0)
                {
                    var properties = propertiesLookup[request.TargetInventoryOwner];
                    while (amountToAdd > 0 && inventoryBuffer.Length < properties.Capacity)
                    {
                        int amountForNewStack = Mathf.Min(amountToAdd, itemData.maxStack);
                        inventoryBuffer.Add(new InventoryItemElement { ItemID = request.ItemID, Amount = amountForNewStack });
                        amountToAdd -= amountForNewStack;
                    }
                }
            }
            ecb.DestroyEntity(requestEntity);
        }).Run();

        // Обработка зпросов на удаление
        Entities.ForEach((Entity requestEntity, in RemoveItemRequest request) =>
        {
            if (inventoryLookup.HasBuffer(request.TargetInventoryOwner))
            {
                var inventoryBuffer = inventoryLookup[request.TargetInventoryOwner];
                int amountToRemove = request.Amount;

                // Итерируемся с конца, чтобы безопасно удалять элементы.
                for (int i = inventoryBuffer.Length - 1; i >= 0; i--)
                {
                    if (amountToRemove <= 0) break;

                    if (inventoryBuffer[i].ItemID == request.ItemID)
                    {
                        var element = inventoryBuffer[i];
                        int amountToTake = Mathf.Min(amountToRemove, element.Amount);
                        
                        element.Amount -= amountToTake;
                        amountToRemove -= amountToTake;

                        if (element.Amount <= 0)
                        {
                            inventoryBuffer.RemoveAt(i);
                        }
                        else
                        {
                            inventoryBuffer[i] = element;
                        }
                    }
                }
            }
            ecb.DestroyEntity(requestEntity);
        }).Schedule();
        
        Dependency.Complete();
    }
}