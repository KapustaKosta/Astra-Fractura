using Unity.Entities;
using UnityEngine;

/// <summary>
/// Authoring-компонент, который наделяет сущность способностью добывать ресурсы.
/// Этот компонент отвечает только за параметры добычи и не содержит логики AI.
/// Для создания NPC-добытчика используйте этот компонент вместе с NPCAuthoring.
/// </summary>
public class HarvesterAuthoring : MonoBehaviour
{
    [Header("Настройки добычи")]
    [Tooltip("Интервал между 'тиками' добычи ресурсов в секундах.")]
    public float harvestInterval = 1.0f;

    [Tooltip("Максимальное расстояние от цели для начала и продолжения добычи.")]
    public float interactionRange = 4.0f;
    
    /// <summary>
    /// Baker преобразует данные из этого MonoBehaviour в компоненты ECS.
    /// </summary>
    private class Baker : Baker<HarvesterAuthoring>
    {
        public override void Bake(HarvesterAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Добавляем компонент с настройками, которые не меняются в процессе игры.
            AddComponent(entity, new HarvesterSettings
            {
                HarvestInterval = authoring.harvestInterval,
                InteractionRange = authoring.interactionRange
            });
            
            // Добавляем компонент для хранения изменяемого состояния процесса добычи.
            // Инициализируем время последней добычи отрицательным значением, чтобы разрешить добычу сразу.
            AddComponent(entity, new HarvesterState { LastHarvestTime = -1f });
        }
    }
}