using Unity.Entities;
using Unity.Collections;
using UnityEngine;

/// <summary>
/// Управляет всеми процессами, связанными с очередью крафта в игре.
/// Система работает в два этапа: сначала обрабатывает новые запросы на добавление в очередь,
/// а затем обновляет состояние активного крафта для всех сущностей, у которых есть очередь.
/// Выполняется после InventorySystem, чтобы корректно работать с инвентарем после всех его изменений за кадр.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(InventorySystem))]
public partial class CraftingProcessingSystem : SystemBase
{
    /// <summary>
    /// Основной метод, вызываемый каждый кадр для выполнения логики системы.
    /// </summary>
    protected override void OnUpdate()
    {
        // Создаем буфер для отложенного выполнения структурных изменений
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        
        // Получаем Lookup-таблицу для быстрого доступа к инвентарям сущностей 
        var inventoryLookup = GetBufferLookup<InventoryItemElement>(true);

        // Находим все одноразовые сущности-запросы, содержащие информацию о крафте и список требуемых ресурсов.
        foreach (var (request, requirements, requestEntity) in SystemAPI.Query<RefRO<StartCraftingRequest>, DynamicBuffer<RequiredCraftingItem>>().WithEntityAccess())
        {
            var crafter = request.ValueRO.Crafter;
            // Выполняем проверку на существование сущности, которая инициировала крафт.
            // Если ее больше нет, удаляем запрос и переходим к следующему.
            if (!SystemAPI.Exists(crafter))
            {
                ecb.DestroyEntity(requestEntity);
                continue;
            }
            
            // Проверяем, достаточно ли у сущности ресурсов для выполнения крафта.
            if (CanCraft(crafter, requirements, inventoryLookup))
            {
                // Если ресурсов достаточно, создаем запросы на их изъятие из инвентаря.
                // Сама логика изъятия делегируется системе InventorySystem, которая обработает эти запросы.
                foreach (var requiredItem in requirements)
                {
                    var removeItemRequest = ecb.CreateEntity();
                    ecb.AddComponent(removeItemRequest, new RemoveItemRequest
                    {
                        TargetInventoryOwner = crafter,
                        ItemID = requiredItem.ItemID,
                        Amount = requiredItem.Amount
                    });
                }
                
                // Получаем буфер очереди крафта у сущности. Если буфера нет, создаем новый.
                var queueBuffer = SystemAPI.HasBuffer<CraftingQueueElement>(crafter)
                    ? SystemAPI.GetBuffer<CraftingQueueElement>(crafter)
                    : ecb.AddBuffer<CraftingQueueElement>(crafter);
                
                // Добавляем новый предмет в конец очереди крафта со всеми необходимыми данными.
                queueBuffer.Add(new CraftingQueueElement
                {
                    TimeRemaining = request.ValueRO.CraftingTime,
                    TotalCraftingTime = request.ValueRO.CraftingTime,
                    ResultItemID = request.ValueRO.ResultItemID,
                    ResultAmount = request.ValueRO.ResultAmount
                });
            }
            // Уничтожаем обработанный запрос, чтобы он не выполнялся повторно в следующем кадре.
            ecb.DestroyEntity(requestEntity);
        }
        
        float deltaTime = SystemAPI.Time.DeltaTime;
        
        // Создаем запрос для поиска всех сущностей, у которых есть активная очередь крафта.
        var query = SystemAPI.QueryBuilder().WithAll<CraftingQueueElement>().Build();
        
        // Итерируемся по массиву сущностей, а не по компонентам.
        // Этот подход необходим для получения изменяемой ссылки на буфер внутри цикла.
        foreach (var crafterEntity in query.ToEntityArray(Allocator.Temp))
        {
            // Получаем изменяемый буфер очереди для текущей сущности.
            var queueBuffer = SystemAPI.GetBuffer<CraftingQueueElement>(crafterEntity);

            // Если в очереди нет предметов, переходим к следующей сущности.
            if (queueBuffer.IsEmpty) continue;

            // Логика крафта всегда обрабатывает только первый элемент в очереди 
            var currentCraft = queueBuffer.ElementAt(0);
            currentCraft.TimeRemaining -= deltaTime;

            // Проверяем, завершился ли крафт.
            if (currentCraft.TimeRemaining <= 0)
            {
                // Если крафт завершен, создаем запрос на добавление готового предмета в инвентарь.
                // Логика добавления делегируется системе InventorySystem.
                var addItemRequest = ecb.CreateEntity();
                ecb.AddComponent(addItemRequest, new AddItemRequest
                {
                    TargetInventoryOwner = crafterEntity,
                    ItemID = currentCraft.ResultItemID,
                    Amount = currentCraft.ResultAmount
                });

                // Удаляем завершенный элемент из начала очереди.
                // Это автоматически делает следующий элемент (если он есть) активным для крафта в следующем кадре.
                queueBuffer.RemoveAt(0);
            }
            else
            {
                // Если крафт еще не завершен, необходимо перезаписать элемент в буфере.
                // Это связано с тем, что 'currentCraft' является структурой (value type), и мы работали с ее копией.
                // Эта строка сохраняет измененное значение 'TimeRemaining' обратно в буфер.
                queueBuffer[0] = currentCraft;
            }
        }
    }
    
    /// <summary>
    /// Проверяет, обладает ли сущность-крафтер достаточным количеством ресурсов для создания предмета.
    /// </summary>
    /// <param name="crafter">Сущность, чей инвентарь проверяется.</param>
    /// <param name="requirements">Буфер с перечнем требуемых ресурсов и их количества.</param>
    /// <param name="inventoryLookup">Lookup-таблица для доступа к инвентарям.</param>
    /// <returns>True, если всех ресурсов достаточно. False, если хотя бы одного ресурса не хватает.</returns>
    private bool CanCraft(Entity crafter, in DynamicBuffer<RequiredCraftingItem> requirements, in BufferLookup<InventoryItemElement> inventoryLookup)
    {
        if (!inventoryLookup.HasBuffer(crafter)) return false;
        
        var inventory = inventoryLookup[crafter];
        
        // Проходим по каждому требованию из рецепта.
        foreach (var requiredItem in requirements)
        {
            int ownedAmount = 0;
            // Суммируем количество требуемого предмета во всех слотах инвентаря.
            foreach (var inventoryItem in inventory)
            {
                if (inventoryItem.ItemID == requiredItem.ItemID)
                {
                    ownedAmount += inventoryItem.Amount;
                }
            }
            // Если имеющееся количество меньше требуемого, немедленно прекращаем проверку.
            if (ownedAmount < requiredItem.Amount)
            {
                return false;
            }
        }
        
        // Если цикл завершился, значит, все проверки пройдены успешно.
        return true;
    }
}