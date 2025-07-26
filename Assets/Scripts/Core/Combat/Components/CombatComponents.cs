using Unity.Entities;

/// <summary>
/// Компонент, хранящий информацию о здоровье сущности.
/// </summary>
public struct HealthComponent : IComponentData
{
    public float MaxHealth;
    public float CurrentHealth;
}

/// <summary>
/// Компонент для отслеживания времени последней атаки (для кулдауна).
/// </summary>
public struct AttackState : IComponentData
{
    public float LastAttackTime;
}

/// <summary>
/// Одноразовый запрос на выполнение атаки.
/// </summary>
public struct PerformAttackRequest : IRequestCleanup
{
    public Entity Attacker;
    public Entity Target;
}

/// <summary>
/// Тег, который добавляется к сущности, когда ее здоровье падает до 0.
/// </summary>
public struct IsDeadTag : IComponentData { }

/// <summary>
/// Компонент-состояние, указывающий, что сущность находится в бою.
/// Хранит время последнего полученного урона для определения выхода из боя по таймауту.
/// </summary>
public struct InCombat : IComponentData
{
    public float LastDamageTime;
}

/// <summary>
/// Компонент-синглтон, который хранит ссылку на сущность NPC,
/// находящуюся в фокусе боя. UI будет ориентироваться на этот компонент.
/// Реализует IComponentData, чтобы быть валидным ECS-компонентом.
/// </summary>

public struct ActiveCombatTarget : IComponentData
{
    public Entity TargetEntity;
}