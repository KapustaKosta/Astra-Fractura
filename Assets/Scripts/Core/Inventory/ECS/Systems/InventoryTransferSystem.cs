using Unity.Entities;
using UnityEngine;
using Game.Production;
using Game.Workshop;

/// <summary>
/// Перенос предметов между инвентарями (General/Input/Output/WIP).
/// Хинты:
///  - SourceInventoryHint (из какого инвентаря источника забирать)
///  - DestinationInventoryHint (в какой инвентарь приёмника класть)
/// Без хинтов: Output → WIP → Input → General.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class InventoryTransferSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Получаем буфер команд для изменения сущностей
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

        var generalLookup = GetBufferLookup<InventoryItemElement>(false);
        var inputLookup = GetBufferLookup<InputInventorySlot>(false);
        var outputLookup = GetBufferLookup<OutputInventorySlot>(false);
        var wipLookup = GetBufferLookup<WorkshopWIPBufferElement>(false);

        var itemRegistry = ItemRegistry.Instance;
        if (itemRegistry == null) return;

        // Обрабатываем все запросы на перенос
        Entities
            .WithoutBurst()
            .ForEach((Entity reqEntity, in TransferItemsRequest request) =>
            {
                DynamicBuffer<InventoryItemElement> src;
                DynamicBuffer<InventoryItemElement> dst;

                bool hasSrcHint = EntityManager.HasComponent<SourceInventoryHint>(reqEntity);
                bool hasDstHint = EntityManager.HasComponent<DestinationInventoryHint>(reqEntity);

                InventoryType srcType = hasSrcHint
                    ? EntityManager.GetComponentData<SourceInventoryHint>(reqEntity).Type
                    : InventoryType.General;
                InventoryType dstType = hasDstHint
                    ? EntityManager.GetComponentData<DestinationInventoryHint>(reqEntity).Type
                    : InventoryType.General;

                bool okSrc = hasSrcHint
                    ? InventoryBufferUtils.TryGetInventoryBufferByType(generalLookup, inputLookup, outputLookup, wipLookup, request.SourceOwner, srcType, out src)
                    : InventoryBufferUtils.TryGetInventoryBufferSmart(generalLookup, inputLookup, outputLookup, wipLookup, request.SourceOwner, out src);

                bool okDst = hasDstHint
                    ? InventoryBufferUtils.TryGetInventoryBufferByType(generalLookup, inputLookup, outputLookup, wipLookup, request.DestinationOwner, dstType, out dst)
                    : InventoryBufferUtils.TryGetInventoryBufferSmart(generalLookup, inputLookup, outputLookup, wipLookup, request.DestinationOwner, out dst);

                if (!okSrc || !okDst) { ecb.DestroyEntity(reqEntity); return; }

                ecb.AddComponent<InventoryChangedTag>(request.SourceOwner);
                ecb.AddComponent<InventoryChangedTag>(request.DestinationOwner);

                for (int i = 0; i < src.Length; i++)
                {
                    var it = src[i];
                    if (it.ItemID == 0 || it.Amount == 0) continue;

                    var data = itemRegistry.GetItemData(it.ItemID);
                    if (data == null) continue;

                    int left = it.Amount;

                    // в существующие стаки
                    for (int j = 0; j < dst.Length && left > 0; j++)
                    {
                        var t = dst[j];
                        if (t.ItemID == it.ItemID && t.Amount < data.maxStack)
                        {
                            int space = data.maxStack - t.Amount;
                            int add = Mathf.Min(left, space);
                            t.Amount += add;
                            dst[j] = t;
                            left -= add;
                        }
                    }

                    // в пустые слоты
                    for (int j = 0; j < dst.Length && left > 0; j++)
                    {
                        if (dst[j].ItemID == 0)
                        {
                            int add = Mathf.Min(left, data.maxStack);
                            dst[j] = new InventoryItemElement { ItemID = it.ItemID, Amount = add };
                            left -= add;
                        }
                    }

                    int moved = it.Amount - left;
                    if (moved > 0)
                    {
                        it.Amount -= moved;
                        src[i] = it.Amount > 0 ? it : default;
                    }
                }

                // Результат
                bool hasLeft = false;
                for (int i = 0; i < src.Length; i++)
                {
                    if (src[i].ItemID != 0 && src[i].Amount > 0) { hasLeft = true; break; }
                }
                if (hasLeft) ecb.AddComponent<TransferFailedTag>(request.SourceOwner);
                else ecb.AddComponent<TransferSuccessTag>(request.SourceOwner);

                ecb.DestroyEntity(reqEntity);
            }).Run();
    }
}

/// <summary>Хинт для источника переноса.</summary>
public struct SourceInventoryHint : IComponentData
{
    public InventoryType Type;
}


