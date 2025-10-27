using Unity.Entities;

/// <summary>
/// Хранит базовые, настраиваемые параметры жизненных показателей NPC,
/// такие как максимальные значения и скорость их убывания.
/// </summary>
public struct NpcVitalsConfig : IComponentData
{
    /// <summary>
    /// Максимальный запас голода.
    /// </summary>
    public float MaxHunger;

    /// <summary>
    /// Скорость уменьшения голода в минуту.
    /// </summary>
    public float HungerDecayPerMinute;

    /// <summary>
    /// Максимальный запас усталости (сил).
    /// </summary>
    public float MaxFatigue;

    /// <summary>
    /// Скорость уменьшения усталости в минуту ВО ВРЕМЯ РАБОТЫ.
    /// </summary>
    public float FatigueDecayPerMinuteWhileWorking;
}

/// <summary>
/// Хранит текущие (динамические) значения жизненных показателей NPC.
/// </summary>
public struct NpcVitalsComponent : IComponentData
{
    /// <summary>
    /// Текущее значение голода.
    /// </summary>
    public float CurrentHunger;

    /// <summary>
    /// Текущее значение усталости.
    /// </summary>
    public float CurrentFatigue;
}