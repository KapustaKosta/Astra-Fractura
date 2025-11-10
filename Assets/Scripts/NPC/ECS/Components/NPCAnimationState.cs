using Unity.Entities;

/// <summary>
/// Компонент для хранения состояния анимации NPC.
/// </summary>
public struct NPCAnimationState : IComponentData
{
    /// <summary>
    /// Текущая скорость движения NPC (для анимации ходьбы/бега).
    /// </summary>
    public float Speed;

    /// <summary>
    /// Флаг, указывающий, что NPC находится в процессе сбора ресурсов.
    /// </summary>
    public bool IsHarvesting;

    /// <summary>
    /// Триггер для анимации атаки. Устанавливается в true на один кадр.
    /// </summary>
    public bool AttackTrigger;
}
