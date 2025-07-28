using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Содержит базовые характеристики NPC, такие как имя, возраст и личные качества.
/// </summary>
public struct NPCComponent : IComponentData
{
    public FixedString64Bytes Name;
    public int Age;
    public FixedString128Bytes Skills;
    public int Organizedness;
    public int Loyalty;
    public int Diligence;
    public Entity Target; // Используется UI для отображения текущей цели
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
    /// Скорость поворота NPC.
    /// </summary>
    public float RotationSpeed;

    /// <summary>
    /// Дистанция остановки от цели.
    /// </summary>
    public float StoppingDistance;

    /// <summary>
    /// Квадрат порогового значения скорости для обнуления.
    /// </summary>
    public float VelocityZeroingThresholdSq;

    /// <summary>
    /// Целевая позиция, к которой движется NPC.
    /// </summary>
    public float3 TargetPosition;

    /// <summary>
    /// Флаг, указывающий, есть ли у NPC активная цель для движения.
    /// </summary>
    public bool HasTarget;
}

/// <summary>
/// Тег, указывающий, что NPC нанят игроком и выполняет его задачи.
/// </summary>
public struct NPCHiredTag : IComponentData { }

/// <summary>
/// Хранит базовые, неизменяемые статы движения NPC, 
/// заданные в Authoring-компоненте. Используется для восстановления
/// стандартной дистанции остановки после выполнения специфических задач.
/// </summary>
public struct NPCBaseMovementStats : IComponentData
{
    public float StoppingDistance;
}
