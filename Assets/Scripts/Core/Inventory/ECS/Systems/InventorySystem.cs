using Unity.Entities;
using UnityEngine;
using Game.Production;
using Game.Workshop;

/// <summary>
/// AddItemRequest / RemoveItemRequest для любых типов инвентарей (General/Input/Output/WIP).
/// Можно подсказать тип через DestinationInventoryHint на сущности запроса.
/// Без хинта применяется порядок Output → WIP → Input → General.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class InventorySystem : SystemBase
{
    /// <summary>
    /// Вызывается каждый кадр для обработки всех ожидающих запросов на изменение инвентаря.
    /// </summary>
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

        var generalLookup = GetBufferLookup<InventoryItemElement>(false);
        var inputLookup = GetBufferLookup<InputInventorySlot>(false);
        var outputLookup = GetBufferLookup<OutputInventorySlot>(false);
        var wipLookup = GetBufferLookup<WorkshopWIPBufferElement>(false);

        var itemRegistry = ItemRegistry.Instance;
        if (itemRegistry == null) return;

        // ADD 
        Entities
            .WithoutBurst()
            .ForEach((Entity reqEntity, ref AddItemRequest request) =>
            {
                DynamicBuffer<InventoryItemElement> buffer;

                bool useType = EntityManager.HasComponent<DestinationInventoryHint>(reqEntity);
                InventoryType targetType = useType
                    ? EntityManager.GetComponentData<DestinationInventoryHint>(reqEntity).Type
                    : InventoryType.General;

                bool ok = useType
                    ? InventoryBufferUtils.TryGetInventoryBufferByType(generalLookup, inputLookup, outputLookup, wipLookup, request.TargetInventoryOwner, targetType, out buffer)
                    : InventoryBufferUtils.TryGetInventoryBufferSmart(generalLookup, inputLookup, outputLookup, wipLookup, request.TargetInventoryOwner, out buffer);

                if (!ok) { ecb.DestroyEntity(reqEntity); return; }

                // Добавляем тег, так как инвентарь будет изменен.
                ecb.AddComponent<InventoryChangedTag>(request.TargetInventoryOwner);

                int toAdd = request.Amount, actuallyAdded = 0;

                // 1) в существующие стаки
                for (int i = 0; i < buffer.Length && toAdd > 0; i++)
                {
                    var el = buffer[i];
                    if (el.ItemID != request.ItemID) continue;

                    var data = itemRegistry.GetItemData(request.ItemID);
                    if (data == null) break;

                    int space = data.maxStack - el.Amount;
                    if (space <= 0) continue;

                    int add = Mathf.Min(toAdd, space);
                    el.Amount += add;
                    buffer[i] = el;
                    toAdd -= add;
                    actuallyAdded += add;
                }

                // 2) в пустые слоты
                for (int i = 0; i < buffer.Length && toAdd > 0; i++)
                {
                    if (buffer[i].ItemID == 0)
                    {
                        var data = itemRegistry.GetItemData(request.ItemID);
                        if (data == null) break;

                        int add = Mathf.Min(toAdd, data.maxStack);
                        buffer[i] = new InventoryItemElement { ItemID = request.ItemID, Amount = add };
                        toAdd -= add;
                        actuallyAdded += add;
                    }
                }

                request.Amount = actuallyAdded;
                ecb.DestroyEntity(reqEntity);
            }).Run();

        // REMOVE 
        Entities
            .WithoutBurst()
            .ForEach((Entity reqEntity, ref RemoveItemRequest request) =>
            {
                DynamicBuffer<InventoryItemElement> buffer;

                bool useType = EntityManager.HasComponent<DestinationInventoryHint>(reqEntity);
                InventoryType targetType = useType
                    ? EntityManager.GetComponentData<DestinationInventoryHint>(reqEntity).Type
                    : InventoryType.General;

                bool ok = useType
                    ? InventoryBufferUtils.TryGetInventoryBufferByType(generalLookup, inputLookup, outputLookup, wipLookup, request.TargetInventoryOwner, targetType, out buffer)
                    : InventoryBufferUtils.TryGetInventoryBufferSmart(generalLookup, inputLookup, outputLookup, wipLookup, request.TargetInventoryOwner, out buffer);

                if (!ok) { ecb.DestroyEntity(reqEntity); return; }

                // Добавляем тег, так как инвентарь будет изменен.
                ecb.AddComponent<InventoryChangedTag>(request.TargetInventoryOwner);

                int toRemove = request.Amount, removed = 0;

                for (int i = 0; i < buffer.Length && toRemove > 0; i++)
                {
                    var el = buffer[i];
                    if (el.ItemID != request.ItemID || el.Amount <= 0) continue;

                    int take = Mathf.Min(toRemove, el.Amount);
                    el.Amount -= take;
                    buffer[i] = el.Amount > 0 ? el : default;

                    toRemove -= take;
                    removed += take;
                }

                request.Amount = removed;
                ecb.DestroyEntity(reqEntity);
            }).Run();
    }
}

/// <summary>
/// Необязательный хинт для Add/Remove/Transfer ─ какой именно инвентарь приёмника использовать.
/// </summary>
public struct DestinationInventoryHint : IComponentData
{
    public InventoryType Type;
}