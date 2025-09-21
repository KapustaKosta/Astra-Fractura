using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

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
/// ECS-компонент, хранящий динамические данные, связанные с движением NPC (могут изменяться в рантайме).
/// </summary>
public struct NPCMovementComponent : IComponentData
{
    public float Speed; 
    public float RotationSpeed; 
    public float StoppingDistance;
    public float VelocityZeroingThresholdSq;
    
    //  Разделяем данные для систем
    public float3 TargetPosition;           
    public float3 TargetOffset;            
    
    public bool HasTarget;
    public float3 CurrentDesiredMoveDirection; 
    public float3 SmoothedMoveDirection; 
    
    public float3 PreferredVelocity;
    // Финальная безопасная скорость после расчетов ORCA (используется системой движения)
    public float3 TargetVelocity;
}

/// <summary>
/// Тег, указывающий, что NPC нанят игроком и выполняет его задачи.
/// </summary>
public struct NPCHiredTag : IComponentData { }

/// <summary>
/// Хранит базовые, неизменяемые статы движения NPC.
/// </summary>  
public struct NPCBaseMovementStats : IComponentData
{
    public float Speed;
    public float RotationSpeed;
    public float StoppingDistance;
}