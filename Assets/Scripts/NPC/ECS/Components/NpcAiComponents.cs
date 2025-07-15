using Unity.Entities;

/// <summary>
/// Перечисление всех возможных типов глобальных целей для NPC.
/// </summary>
public enum GoalType { Idle, Harvest, ReturnToBase, Flee }

/// <summary>
/// Компонент, указывающий на текущую активную цель NPC. Является unmanaged.
/// </summary>
public struct ActiveGoal : IComponentData
{
    public GoalType Type;
    public Entity Target;
    public int RelevantItemID;
    public float CurrentGoalScore;
}


public struct NPCBrain : IComponentData { }
public struct AvailableAction : IBufferElementData
{
    public GoalType Type;
    public float BaseScore;
}
public struct MoveToRequest : IComponentData
{
    public Entity TargetEntity;
    public float StoppingDistance;
}

public struct AISettings : IComponentData
{
    public float AISearchRadius;
    public int ResourceCollisionLayer;
    public float PlayerAssignHarvestPriority;
    public float PlayerAssignReturnPriority;
    public float RotationSpeed;
    public float HarvestInteractionRangeBuffer;
    public float ReturnToBaseStoppingDistanceBuffer;
}