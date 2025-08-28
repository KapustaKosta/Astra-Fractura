using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Game.Production;
using Game.Workshop;

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

        var elementLookup = GetBufferLookup<InventoryItemElement>(false);
        var inputLookup = GetBufferLookup<InputInventorySlot>(false);
        var outputLookup = GetBufferLookup<OutputInventorySlot>(false);
        var wipLookup = GetBufferLookup<WorkshopWIPBufferElement>(false);

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

                DynamicBuffer<InventoryItemElement> sourceBuffer;
                DynamicBuffer<InventoryItemElement> destBuffer;

                // Получаем буфер источника (пока без хинтов, так как игрок - General)
                // В будущем здесь можно добавить проверку SourceInventoryHint
                bool sourceOk = InventoryBufferUtils.TryGetInventoryBuffer(elementLookup, inputLookup, outputLookup, wipLookup, request.SourceInventoryOwner, out sourceBuffer);

                bool destOk;
                if (EntityManager.HasComponent<DestinationInventoryHint>(requestEntity))
                {
                    var hint = EntityManager.GetComponentData<DestinationInventoryHint>(requestEntity);
                    destOk = InventoryBufferUtils.TryGetInventoryBufferByType(elementLookup, inputLookup, outputLookup, wipLookup, request.DestinationInventoryOwner, hint.Type, out destBuffer);
                }
                else
                {
                    // Стандартная логика, если хинта нет
                    destOk = InventoryBufferUtils.TryGetInventoryBuffer(elementLookup, inputLookup, outputLookup, wipLookup, request.DestinationInventoryOwner, out destBuffer);
                }

                if (!sourceOk || !destOk)
                {
                    ecb.DestroyEntity(requestEntity);
                    return;
                }


                ecb.AddComponent<InventoryChangedTag>(request.SourceInventoryOwner);
                ecb.AddComponent<InventoryChangedTag>(request.DestinationInventoryOwner);

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
                if (itemData == null)
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
                else if (destItem.ItemID == 0)
                {
                    destBuffer[request.DestinationSlotIndex] = sourceItem;
                    sourceBuffer[request.SourceSlotIndex] = destItem;
                }

                // 5. Уничтожаем обработанный запрос.
                ecb.DestroyEntity(requestEntity);

            }).Run();
    }
}