using Unity.Entities;
using Unity.Mathematics; 
using UnityEngine; 

/// <summary>
/// Система, отвечающая за обработку запросов на перемещение предметов между слотами.
/// Корректно обрабатывает как простое перемещение и обмен (swap), так и стекирование
/// одинаковых предметов с учетом максимального размера стака.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class MoveItemSystem : SystemBase
{
    /// <summary>
    /// Вызывается каждый кадр для обработки запросов на перемещение.
    /// </summary>
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        var bufferLookup = GetBufferLookup<InventoryItemElement>(false); 
        var itemRegistry = ItemRegistry.Instance; 

        if (itemRegistry == null)
        {
            // Если реестр недоступен, мы не можем работать.
            // Уничтожаем все запросы, чтобы они не висели вечно.
            var requestQuery = SystemAPI.QueryBuilder().WithAll<MoveItemRequest>().Build();
            ecb.DestroyEntity(requestQuery, EntityQueryCaptureMode.AtPlayback);
            return;
        }

        Entities
            .WithoutBurst()
            .ForEach((Entity requestEntity, in MoveItemRequest request) =>
            {
                // 1. Проверяем валидность данных (существование инвентарей и корректность индексов).
                if (!bufferLookup.HasBuffer(request.SourceInventoryOwner) ||
                    !bufferLookup.HasBuffer(request.DestinationInventoryOwner))
                {
                    ecb.DestroyEntity(requestEntity);
                    return;
                }

                // Добавляем теги обоим инвентарям, так как они оба меняются.
                ecb.AddComponent<InventoryChangedTag>(request.SourceInventoryOwner);
                ecb.AddComponent<InventoryChangedTag>(request.DestinationInventoryOwner);

                var sourceBuffer = bufferLookup[request.SourceInventoryOwner];
                var destBuffer = bufferLookup[request.DestinationInventoryOwner];

                if (request.SourceSlotIndex >= sourceBuffer.Length || request.DestinationSlotIndex >= destBuffer.Length || request.SourceSlotIndex < 0 || request.DestinationSlotIndex < 0)
                {
                    ecb.DestroyEntity(requestEntity);
                    return;
                }

                // 2. Получаем текущее состояние слотов.
                var sourceItem = sourceBuffer[request.SourceSlotIndex];
                var destItem = destBuffer[request.DestinationSlotIndex];

                // 3. Проверяем, что мы все еще тащим тот предмет, с которого начали.
                if (sourceItem.ItemID != request.ItemID || sourceItem.Amount == 0)
                {
                    ecb.DestroyEntity(requestEntity);
                    return;
                }
                
                var itemData = itemRegistry.GetItemData(sourceItem.ItemID);
                if(itemData == null)
                {
                    ecb.DestroyEntity(requestEntity);
                    return;
                }

                // Логика стекирования и обмена 

                // Случай 1: Стекирование. Происходит, если предметы одинаковые и в слоте назначения есть место.
                if (sourceItem.ItemID == destItem.ItemID && destItem.Amount < itemData.maxStack)
                {
                    int spaceAvailable = itemData.maxStack - destItem.Amount;
                    int amountToMove = math.min(sourceItem.Amount, spaceAvailable);

                    // Добавляем в слот назначения
                    destItem.Amount += amountToMove;
                    destBuffer[request.DestinationSlotIndex] = destItem;

                    // Убираем из исходного слота
                    sourceItem.Amount -= amountToMove;
                    if (sourceItem.Amount <= 0)
                    {
                        // Если исходный стак опустел, очищаем слот
                        sourceBuffer[request.SourceSlotIndex] = new InventoryItemElement { ItemID = 0, Amount = 0 };
                    }
                    else
                    {
                        // Иначе просто обновляем количество
                        sourceBuffer[request.SourceSlotIndex] = sourceItem;
                    }
                }
                // Случай 2: Простой обмен (Swap). Происходит во всех остальных случаях:
                // - Перемещение на пустой слот (destItem.ItemID == 0)
                // - Перемещение на другой предмет (sourceItem.ItemID != destItem.ItemID)
                // - Перемещение на полный стак того же предмета
                else
                {
                    destBuffer[request.DestinationSlotIndex] = sourceItem;
                    sourceBuffer[request.SourceSlotIndex] = destItem;
                }

                // 5. Уничтожаем обработанный запрос.
                ecb.DestroyEntity(requestEntity);
                
            }).Run();
    }
}