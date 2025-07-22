using UnityEngine;

/// <summary>
/// ScriptableObject для хранения настроек визуального отображения квикбара.
/// Позволяет настраивать цвета для активного и неактивного состояния слотов в редакторе Unity.
/// </summary>
[CreateAssetMenu(fileName = "QuickbarSettings", menuName = "UI/Quickbar Settings")]
public class QuickbarSettings : ScriptableObject
{
    [Header("Slot Colors")]
    [Tooltip("Цвет фона для активного, выбранного слота.")]
    public Color activeSlotColor = Color.white;

    [Tooltip("Цвет фона для неактивных слотов.")]
    public Color inactiveSlotColor = new Color(0.5f, 0.5f, 0.5f, 1f); // 50% серый
}