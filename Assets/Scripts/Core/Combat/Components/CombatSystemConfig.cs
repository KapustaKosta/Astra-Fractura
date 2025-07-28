using Unity.Entities;

/// <summary>
/// Компонент-синглтон для хранения глобальных настроек, связанных с боевой системой.
/// </summary>
public struct CombatSystemConfig : IComponentData
{
    public float CombatTimeoutDuration;
}