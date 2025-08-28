using UnityEngine;

/// <summary>
/// ScriptableObject representing a wire item. Inherits from the base Item class and adds
/// fields for wire level and capacity. Use this to define each wire level in the project.
/// </summary>
[CreateAssetMenu(fileName = "WireItem", menuName = "Items/Wire Item")]
public class WireItem : Item
{
    [Header("Wire Settings")]
    [Tooltip("Уровень провода (1..N), определяющий пропускную способность.")]
    public int wireLevel = 1;

    [Tooltip("Максимальная пропускная способность этого уровня, кВт.")]
    public float capacityKW = 2f;
}
