using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Authoring-компонент для определения параметров движения NPC в ECS.
/// Позволяет настраивать скорость движения NPC в редакторе Unity.
/// </summary>
[DisallowMultipleComponent]
public class NPCMovementAuthoring : MonoBehaviour
{
    /// <summary>
    /// Скорость движения NPC.
    /// </summary>
    [Header("Movement Settings")]
    public float Speed = 2.0f;

    /// <summary>
    /// Baker-класс для преобразования NPCMovementAuthoring в ECS-компоненты.
    /// </summary>
    public class Baker : Baker<NPCMovementAuthoring>
    {
        /// <summary>
        /// Выполняет процесс "запекания" данных из MonoBehaviour в ECS-сущности.
        /// Создает и добавляет компонент NPCMovementComponent к сущности NPC.
        /// </summary>
        /// <param name="authoring">Экземпляр NPCMovementAuthoring.</param>
        public override void Bake(NPCMovementAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic); 
            AddComponent(entity, new NPCMovementComponent
            {
                Speed = authoring.Speed,
                TargetPosition = float3.zero,
                HasTarget = false
            });
        }
    }
}

/// <summary>
/// ECS-компонент, хранящий данные, связанные с движением NPC.
/// </summary>
public struct NPCMovementComponent : IComponentData
{
    /// <summary>
    /// Скорость перемещения NPC.
    /// </summary>
    public float Speed;

    /// <summary>
    /// Целевая позиция, к которой движется NPC.
    /// </summary>
    public float3 TargetPosition;

    /// <summary>
    /// Флаг, указывающий, есть ли у NPC активная цель для движения.
    /// </summary>
    public bool HasTarget;
}