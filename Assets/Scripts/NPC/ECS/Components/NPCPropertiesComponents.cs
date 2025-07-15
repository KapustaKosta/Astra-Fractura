using Unity.Collections;
using Unity.Entities;

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
/// Хранит параметры движения, специфичные для NPC.
/// </summary>
public struct NPCMovementComponent : IComponentData
{
    public float Speed;
    public float StoppingDistance;
}

/// <summary>
/// Тег, указывающий, что NPC нанят игроком и выполняет его задачи.
/// </summary>
public struct NPCHiredTag : IComponentData { }