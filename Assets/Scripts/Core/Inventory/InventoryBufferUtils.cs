using Unity.Collections;
using Unity.Entities;
using Game.Production;
using Game.Workshop;
using UnityEngine;

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
        Debug.Log($"<color=magenta>[InventoryBufferUtils]</color> Trying to get buffer for Owner: {owner}, Type: {type}");

        switch (type)
        {
            case InventoryType.General:
                if (elementLookup.HasBuffer(owner)) { buffer = elementLookup[owner]; return true; }
                break;
            case InventoryType.Input:
                if (inputLookup.HasBuffer(owner))
                {
                    Debug.Log("<color=magenta>[InventoryBufferUtils]</color> Found InputInventorySlot buffer. Reinterpreting.");
                    buffer = inputLookup[owner].Reinterpret<InventoryItemElement>();
                    return true;
                }
                else
                {
                    Debug.LogWarning($"<color=magenta>[InventoryBufferUtils]</color> Type is Input, but entity {owner} does NOT have InputInventorySlot buffer!");
                }
                break;
            case InventoryType.Output:
                if (outputLookup.HasBuffer(owner))
                {
                    Debug.Log("<color=magenta>[InventoryBufferUtils]</color> Found OutputInventorySlot buffer. Reinterpreting.");
                    buffer = outputLookup[owner].Reinterpret<InventoryItemElement>();
                    return true;
                }
                else
                {
                    Debug.LogWarning($"<color=magenta>[InventoryBufferUtils]</color> Type is Output, but entity {owner} does NOT have OutputInventorySlot buffer!");
                }
                break;
            case InventoryType.WIP:
                if (wipLookup.HasBuffer(owner))
                {
                    Debug.Log("<color=magenta>[InventoryBufferUtils]</color> Found WorkshopWIPBufferElement buffer. Reinterpreting.");
                    buffer = wipLookup[owner].Reinterpret<InventoryItemElement>();
                    return true;
                }
                else
                {
                    Debug.LogWarning($"<color=magenta>[InventoryBufferUtils]</color> Type is WIP, but entity {owner} does NOT have WorkshopWIPBufferElement buffer!");
                }
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