using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

[DisallowMultipleComponent]
public class NPCMovementAuthoring : MonoBehaviour
{
    [Header("Movement Settings")]
    public float Speed = 2.0f; // Скорость движения NPC

    // Класс для конвертации в ECS-компонент
    public class Baker : Baker<NPCMovementAuthoring>
    {
        public override void Bake(NPCMovementAuthoring authoring)
        {
            AddComponent(new NPCMovementComponent
            {
                Speed = authoring.Speed,
                TargetPosition = float3.zero, // По умолчанию цель отсутствует
                HasTarget = false
            });
        }
    }
}

public struct NPCMovementComponent : IComponentData
{
    public float Speed; // Скорость движения
    public float3 TargetPosition; // Позиция цели
    public bool HasTarget; // Флаг, есть ли цель
}
