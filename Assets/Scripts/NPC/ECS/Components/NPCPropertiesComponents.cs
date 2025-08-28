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
    public Entity AssignedWorkshop; // Цех, за который отвечает NPC
}

/// <summary>
/// Хранит запас "рабочей силы" (молотков), который NPC может потратить за один рабочий цикл.
/// </summary>
public struct NPCWorkForce : IComponentData
{
    /// <summary>
    /// Максимальный запас "молотков", который восстанавливается в начале каждого цикла.
    /// </summary>
    public float MaxHammerPool;
    /// <summary>
    /// Текущий остаток "молотков" в данном рабочем цикле.
    /// </summary>
    public float CurrentHammerPool;
}

/// <summary>
/// ECS-компонент, хранящий данные, связанные с движением NPC.
/// </summary>
public struct NPCMovementComponent : IComponentData
{
    public float Speed;
    public float RotationSpeed;
    public float StoppingDistance;
    public float VelocityZeroingThresholdSq;
    public float3 TargetPosition;
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