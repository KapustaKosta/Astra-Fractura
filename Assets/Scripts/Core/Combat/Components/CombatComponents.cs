// Assets/Scripts/Core/Combat/Components/CombatComponents.cs
using Unity.Entities;
using Unity.Mathematics;

/// <summary>Здоровье.</summary>
public struct HealthComponent : IComponentData
{
    public float MaxHealth;
    public float CurrentHealth;
}

/// <summary>Состояние атаки (кулдаун).</summary>
public struct AttackState : IComponentData
{
    public float LastAttackTime;
}

/// <summary>Одноразовый запрос на атаку.</summary>
public struct PerformAttackRequest : IRequestCleanup
{
    public Entity Attacker;
    public Entity Target;
    public float3 AttackerPosition;
}

/// <summary>Метка смерти.</summary>
public struct IsDeadTag : IComponentData { }

/// <summary>В бою.</summary>
public struct InCombat : IComponentData
{
    public float LastDamageTime;
}

/// <summary>Активная цель боя для UI и пр.</summary>
public struct ActiveCombatTarget : IComponentData
{
    public Entity TargetEntity;
}

/// <summary>
/// [NEW] Последний "хит" по сущности — позиция/направление удара, время и урон.
/// Используется DeathSystem для корректного импульса в сторону удара.
/// </summary>
public struct LastHitInfo : IComponentData
{
    public float3 AttackerPosition;  // откуда били
    public float3 HitPoint;          // куда попали (верх корпуса)
    public float3 HitDirection;      // нормализованное направление удара
    public float  Damage;
    public float  Time;
}