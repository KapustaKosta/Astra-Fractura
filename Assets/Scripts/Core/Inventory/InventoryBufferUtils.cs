using Unity.Collections;
using Unity.Entities;
using Game.Production;
using Game.Workshop;

/// <summary>
/// Утилиты для унифицированного доступа к инвентарным буферам.
/// Позволяет получать любой из поддерживаемых типов буферов как DynamicBuffer<InventoryItemElement>.
/// </summary>
public static class InventoryBufferUtils
{
    /// <summary>
    /// Пытается получить конкретный буфер по требуемому типу.
    /// </summary>
    public static bool TryGetInventoryBufferByType(
        BufferLookup<InventoryItemElement> elementLookup,
        BufferLookup<InputInventorySlot> inputLookup,
        BufferLookup<OutputInventorySlot> outputLookup,
        BufferLookup<WorkshopWIPBufferElement> wipLookup,
        Entity owner,
        InventoryType type,
        out DynamicBuffer<InventoryItemElement> buffer)
    {
        switch (type)
        {
            case InventoryType.General:
                if (elementLookup.HasBuffer(owner)) { buffer = elementLookup[owner]; return true; }
                break;
            case InventoryType.Input:
                if (inputLookup.HasBuffer(owner)) { buffer = inputLookup[owner].Reinterpret<InventoryItemElement>(); return true; }
                break;
            case InventoryType.Output:
                if (outputLookup.HasBuffer(owner)) { buffer = outputLookup[owner].Reinterpret<InventoryItemElement>(); return true; }
                break;
            case InventoryType.WIP:
                if (wipLookup.HasBuffer(owner)) { buffer = wipLookup[owner].Reinterpret<InventoryItemElement>(); return true; }
                break;
        }
        buffer = default;
        return false;
    }

    /// <summary>
    /// «Умный» порядок по умолчанию, подходящий для производственных зданий:
    /// Output → WIP → Input → General.
    /// </summary>
    public static bool TryGetInventoryBufferSmart(
        BufferLookup<InventoryItemElement> elementLookup,
        BufferLookup<InputInventorySlot> inputLookup,
        BufferLookup<OutputInventorySlot> outputLookup,
        BufferLookup<WorkshopWIPBufferElement> wipLookup,
        Entity owner,
        out DynamicBuffer<InventoryItemElement> buffer)
    {
        if (outputLookup.HasBuffer(owner)) { buffer = outputLookup[owner].Reinterpret<InventoryItemElement>(); return true; }
        if (wipLookup.HasBuffer(owner)) { buffer = wipLookup[owner].Reinterpret<InventoryItemElement>(); return true; }
        if (inputLookup.HasBuffer(owner)) { buffer = inputLookup[owner].Reinterpret<InventoryItemElement>(); return true; }
        if (elementLookup.HasBuffer(owner)) { buffer = elementLookup[owner]; return true; }

        buffer = default;
        return false;
    }

    /// <summary>
    /// Старая сигнатура: оставлена для обратной совместимости (использует Smart).
    /// </summary>
    public static bool TryGetInventoryBuffer(
        BufferLookup<InventoryItemElement> elementLookup,
        BufferLookup<InputInventorySlot> inputLookup,
        BufferLookup<OutputInventorySlot> outputLookup,
        BufferLookup<WorkshopWIPBufferElement> wipLookup,
        Entity owner,
        out DynamicBuffer<InventoryItemElement> buffer)
    {
        return TryGetInventoryBufferSmart(elementLookup, inputLookup, outputLookup, wipLookup, owner, out buffer);
    }
}
