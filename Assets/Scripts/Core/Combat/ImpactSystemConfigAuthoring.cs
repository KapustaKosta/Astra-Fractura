using Unity.Entities;
using UnityEngine;

/// <summary>
/// Компонент-синглтон для хранения глобальных настроек физического воздействия от атак.
/// </summary>
public struct ImpactSystemConfig : IComponentData
{
    // Сила, с которой атака игрока отталкивает цель назад.
    public float PlayerAttackKnockback;
    // Сила, с которой атака NPC отталкивает цель назад.
    public float NpcAttackKnockback;
    // Дополнительная сила, подбрасывающая цель вверх при ударе.
    public float KnockbackUpwardForce;
}


/// <summary>
/// MonoBehaviour для настройки параметров отталкивания в редакторе Unity.
/// </summary>
public class ImpactSystemConfigAuthoring : MonoBehaviour
{
    [Tooltip("Сила, с которой атака игрока отталкивает цель назад.")]
    public float playerAttackKnockback = 2.5f;

    [Tooltip("Сила, с которой атака NPC отталкивает цель назад.")]
    public float npcAttackKnockback = 2.0f;

    [Tooltip("Дополнительная сила, подбрасывающая цель вверх при ударе.")]
    public float knockbackUpwardForce = 1.5f;
    
    class Baker : Baker<ImpactSystemConfigAuthoring>
    {
        public override void Bake(ImpactSystemConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new ImpactSystemConfig
            {
                PlayerAttackKnockback = authoring.playerAttackKnockback,
                NpcAttackKnockback = authoring.npcAttackKnockback,
                KnockbackUpwardForce = authoring.knockbackUpwardForce
            });
        }
    }
}