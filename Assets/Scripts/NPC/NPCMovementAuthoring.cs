using UnityEngine;
using Unity.Entities;

/// <summary>
/// Authoring-компонент, который наделяет сущность способностью двигаться.
/// Добавляет компонент NPCMovementComponent с настройками скорости и дистанции остановки.
/// </summary>
[DisallowMultipleComponent]
public class NPCMovementAuthoring : MonoBehaviour
{
    [Header("Настройки движения")]
    [Tooltip("Скорость передвижения сущности в метрах/сек.")]
    public float Speed = 3.5f;
    
    [Tooltip("Расстояние до цели, на котором сущность прекратит движение.")]
    public float StoppingDistance = 1.5f;

    /// <summary>
    /// Baker преобразует данные из этого MonoBehaviour в компоненты ECS.
    /// </summary>
    public class Baker : Baker<NPCMovementAuthoring>
    {
        /// <summary>
        /// Создает и добавляет компонент NPCMovementComponent к сущности NPC.
        /// </summary>
        /// <param name="authoring">Экземпляр NPCMovementAuthoring.</param>
        public override void Bake(NPCMovementAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            
            // Добавляем компонент с параметрами движения.
            // Этот компонент будет использоваться системой NPCMovementSystem.
            AddComponent(entity, new NPCMovementComponent
            {
                Speed = authoring.Speed,
                StoppingDistance = authoring.StoppingDistance
            });
        }
    }
}