using Unity.Entities;

/// <summary>
/// Компонент, определяющий боевые параметры враждебного NPC.
/// </summary>
public struct AttackAIComponent : IComponentData
{
    public float AttackRange;      // Дистанция, с которой NPC может атаковать.
    public float AttackDamage;     // Урон от одной атаки.
    public float AttackCooldown;   // Время в секундах между атаками.
    public float ChaseStopRange;   // Дистанция, на которой NPC прекращает преследование. Должна быть больше AISearchRadius.
}

/// <summary>
/// Динамический компонент, хранящий текущее состояние атаки NPC.
/// </summary>
public struct NPCAttackState : IComponentData
{
    public float CurrentCooldown; // Оставшееся время до следующей возможной атаки.
}

/// <summary>
/// Помечает NPC как враждебного (не нанимаемого, не управляемого игроком).
/// Фильтруется отдельным арбитром врагов.
/// </summary>
public struct HostileNPCTag : IComponentData {}

/// <summary>
/// Компонент-состояние. Добавляется к враждебному NPC, когда игрок
/// оказывается в его радиусе обнаружения.
/// </summary>
public struct PlayerInRangeTag : IComponentData
{
    public Entity PlayerEntity; // Ссылка на сущность игрока.
}

/// <summary>
/// Тег-запрос. Добавляется к NPC, когда он находится в радиусе атаки
/// и готов нанести удар. Потребляется системой NPCDamageSystem.
/// </summary>
public struct AttackRequestTag : IComponentData { }

/// <summary>
/// Предполагаемая структура команды для нанесения урона.
/// Ваша система урона должна будет обрабатывать сущности с этим компонентом.
/// </summary>
public struct ApplyDamageCommand : IComponentData
{
    public Entity Target;
    public float Damage;
}

/// <summary>
/// Тег, указывающий, что NPC находится в радиусе атаки и выполняет атаку.
/// Движение в этом состоянии должно быть остановлено.
/// </summary>
public struct IsAttackingTag : IComponentData { }