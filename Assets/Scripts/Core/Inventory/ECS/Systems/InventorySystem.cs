using Unity.Entities;
using UnityEngine;

/// <summary>
/// Централизованная система для обработки запросов на изменение инвентарей.
/// Обрабатывает запросы на добавление (AddItemRequest) и удаление (RemoveItemRequest) предметов.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class InventorySystem : SystemBase
{
    /// <summary>
    /// Вызывается каждый кадр для обработки всех ожидающих запросов на изменение инвентаря.
    /// </summary>
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        
        var inventoryLookup = GetBufferLookup<InventoryItemElement>(); 
        var itemRegistry = ItemRegistry.Instance;
        if (itemRegistry == null) return;

        // Этап 1: Обработка запросов на добавление предметов
        Entities
            .WithoutBurst()
            .WithChangeFilter<AddItemRequest>() // Оптимизация: система сработает только если появились новые запросы.
            .ForEach((Entity requestEntity, ref AddItemRequest request) =>
            {
                // Проверяем, что у целевой сущности есть инвентарь.
                if (!inventoryLookup.HasBuffer(request.TargetInventoryOwner))
                {
                    ecb.DestroyEntity(requestEntity);
                    return;
                }

                var inventoryBuffer = inventoryLookup[request.TargetInventoryOwner];
                int amountToAdd = request.Amount;
                int actuallyAdded = 0;

                // Сначала пытаемся добавить предметы в уже существующие стаки того же типа.
                for (int i = 0; i < inventoryBuffer.Length && amountToAdd > 0; i++)
                {
                    var element = inventoryBuffer[i];
                    if (element.ItemID == request.ItemID)
                    {
                        var itemData = itemRegistry.GetItemData(request.ItemID);
                        if (itemData == null) continue;
                        
                        int spaceInStack = itemData.maxStack - element.Amount;
                        int transferAmount = Mathf.Min(amountToAdd, spaceInStack);

                        element.Amount += transferAmount;
                        inventoryBuffer[i] = element;
                        
                        amountToAdd -= transferAmount;
                        actuallyAdded += transferAmount;
                    }
                }

                // Затем пытаемся положить оставшиеся предметы в пустые слоты.
                for (int i = 0; i < inventoryBuffer.Length && amountToAdd > 0; i++)
                {
                    if (inventoryBuffer[i].ItemID == 0)
                    {
                        var itemData = itemRegistry.GetItemData(request.ItemID);
                        if (itemData == null) break;

                        int transferAmount = Mathf.Min(amountToAdd, itemData.maxStack);
                        
                        inventoryBuffer[i] = new InventoryItemElement { ItemID = request.ItemID, Amount = transferAmount };

                        amountToAdd -= transferAmount;
                        actuallyAdded += transferAmount;
                    }
                }
                
                // Обновляем поле в запросе, чтобы отразить, сколько предметов было добавлено по факту.
                request.Amount = actuallyAdded; 
                
            }).Run();

        // Этап 2: Обработка запросов на удаление предметов
        Entities
            .WithoutBurst()
            .ForEach((Entity requestEntity, in RemoveItemRequest request) =>
            {
                if (!inventoryLookup.HasBuffer(request.TargetInventoryOwner))
                {
                    ecb.DestroyEntity(requestEntity);
                    return;
                }
                
                var inventoryBuffer = inventoryLookup[request.TargetInventoryOwner];
                int amountToRemove = request.Amount;

                // Итерируем инвентарь с конца, чтобы безопасно удалять элементы.
                for (int i = inventoryBuffer.Length - 1; i >= 0 && amountToRemove > 0; i--)
                {
                    if (inventoryBuffer[i].ItemID == request.ItemID)
                    {
                        var element = inventoryBuffer[i];
                        int amountToTake = Mathf.Min(amountToRemove, element.Amount);
                        
                        element.Amount -= amountToTake;
                        amountToRemove -= amountToTake;

                        // Если стак полностью опустел, очищаем слот.
                        if (element.Amount <= 0)
                        {
                            inventoryBuffer[i] = default; // Эквивалентно new InventoryItemElement { ItemID = 0, Amount = 0 }
                        }
                        else
                        {
                            inventoryBuffer[i] = element;
                        }
                    }
                }
                // Запрос на удаление считается выполненным и уничтожается.
                ecb.DestroyEntity(requestEntity);
            }).Run();
    }
}