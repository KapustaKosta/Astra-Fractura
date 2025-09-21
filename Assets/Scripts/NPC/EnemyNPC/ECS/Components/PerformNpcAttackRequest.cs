using Unity.Entities;


/// <summary>
/// Одноразовый запрос от NPC о нанесении урона игроку.
/// </summary>
public struct PerformNpcAttackRequest : IComponentData, IRequestCleanup
{
    public Entity Attacker;
    public Entity Target;
    public float Damage;
}