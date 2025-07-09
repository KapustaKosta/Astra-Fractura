using Unity.Entities;
using UnityEngine;

/// <summary>
/// Централизованная система для обработки запросов на изменение инвентарей.
/// Обрабатывает запросы на добавление (<c>AddItemRequest</c>) и удаление (<c>RemoveItemRequest</c>)
/// предметов для любой сущности, имеющей инвентарь.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(FinalizeBuildingSystem))]
[UpdateAfter(typeof(ProcessHarvestRequestSystem))]
public partial class InventorySystem : SystemBase
{
    /// <summary>
    /// Вызывается каждый кадр для обработки всех ожидающих запросов на изменение инвентаря.
    /// </summary>
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        
        // Получаем безопасный доступ к буферам инвентаря всех сущностей для их изменения.
        var inventoryLookup = GetBufferLookup<InventoryItemElement>(false); 
        // Получаем доступ к реестру для получения данных о предметах, таких как максимальный размер стака.
        var itemRegistry = ItemRegistry.Instance;

        if (itemRegistry == null)
        {
            return;
        }

        // Обработка запросов на добавление предметов.
        Entities
            .WithoutBurst() 
            .ForEach((Entity requestEntity, in AddItemRequest request) =>
            {
                // Проверяем, что у целевой сущности есть инвентарь.
                if (!inventoryLookup.HasBuffer(request.TargetInventoryOwner))
                {
                    ecb.DestroyEntity(requestEntity);
                    return;
                }

                var inventoryBuffer = inventoryLookup[request.TargetInventoryOwner];
                var itemData = itemRegistry.GetItemData(request.ItemID);
                
                // Проверяем, что предмет с таким ID существует в реестре.
                if (itemData == null)
                {
                    ecb.DestroyEntity(requestEntity);
                    return;
                }
                
                int amountLeftToAdd = request.Amount;

                // Этап 1: Стекирование. Пытаемся добавить предметы в уже существующие, неполные стаки.
                if (itemData.maxStack > 1)
                {
                    for (int i = 0; i < inventoryBuffer.Length; i++)
                    {
                        if (amountLeftToAdd <= 0) break;

                        var element = inventoryBuffer[i];
                        // Ищем слот с таким же предметом, в котором еще есть место.
                        if (element.ItemID == request.ItemID && element.Amount < itemData.maxStack)
                        {
                            int spaceInStack = itemData.maxStack - element.Amount;
                            int amountToMove = Mathf.Min(amountLeftToAdd, spaceInStack);
                            
                            element.Amount += amountToMove;
                            inventoryBuffer[i] = element; 
                            amountLeftToAdd -= amountToMove;
                        }
                    }
                }
                
                // Этап 2: Добавление в новые слоты. Если предметы еще остались, ищем пустые слоты.
                if (amountLeftToAdd > 0)
                {
                    for (int i = 0; i < inventoryBuffer.Length; i++)
                    {
                        if (amountLeftToAdd <= 0) break;

                        // Пустой слот определяется по ItemID равному 0.
                        if (inventoryBuffer[i].ItemID == 0)
                        {
                            int amountForNewStack = Mathf.Min(amountLeftToAdd, itemData.maxStack);
                            inventoryBuffer[i] = new InventoryItemElement { ItemID = request.ItemID, Amount = amountForNewStack };
                            amountLeftToAdd -= amountForNewStack;
                        }
                    }
                }
                
                ecb.DestroyEntity(requestEntity);

            }).Run();

        // Обработка запросов на удаление предметов.
        Entities
            .WithoutBurst()
            .ForEach((Entity requestEntity, in RemoveItemRequest request) =>
            {
                if (inventoryLookup.HasBuffer(request.TargetInventoryOwner))
                {
                    var inventoryBuffer = inventoryLookup[request.TargetInventoryOwner];
                    int amountToRemove = request.Amount;

                    // Итерируемся с конца инвентаря чтобы удаление
                    // из одного слота не повлияло на индексы последующих слотов в той же итерации.
                    for (int i = inventoryBuffer.Length - 1; i >= 0; i--)
                    {
                        if (amountToRemove <= 0) break;

                        if (inventoryBuffer[i].ItemID == request.ItemID)
                        {
                            var element = inventoryBuffer[i];
                            int amountToTake = Mathf.Min(amountToRemove, element.Amount);
                            
                            element.Amount -= amountToTake;
                            amountToRemove -= amountToTake;

                            // Если в стаке не осталось предметов - очищаем слот.
                            // Мы не удаляем сам элемент из буфера, чтобы сохранить фиксированный размер инвентаря.
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