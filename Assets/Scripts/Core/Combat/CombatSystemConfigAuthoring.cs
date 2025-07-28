using Unity.Entities;
using UnityEngine;

/// <summary>
/// MonoBehaviour, который позволяет настроить параметры боевой системы в редакторе Unity
/// и "запечь" их в ECS-компонент CombatSystemConfig.
/// </summary>
public class CombatSystemConfigAuthoring : MonoBehaviour
{
    [Tooltip("Через сколько секунд NPC выйдет из боя, если не получал урон")]
    public float combatTimeoutDuration = 5.0f;

    class Baker : Baker<CombatSystemConfigAuthoring>
    {
        public override void Bake(CombatSystemConfigAuthoring authoring)
        {
            // Создаем сущность-синглтон, которая не привязана к игровому объекту.
            var entity = GetEntity(TransformUsageFlags.None);

            // Добавляем к этой сущности наш компонент с данными, взятыми из полей редактора.
            AddComponent(entity, new CombatSystemConfig
            {
                CombatTimeoutDuration = authoring.combatTimeoutDuration
            });
        }
    }
}