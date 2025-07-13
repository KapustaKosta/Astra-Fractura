using Unity.Entities;
using Unity.Mathematics;

public struct NPCPathfindingComponent : IComponentData
{
    public bool NeedsPathUpdate;
    public float3 LastTargetPosition;
    public int CurrentWaypointIndex;
}

public struct NPCPathBufferElement : IBufferElementData
{
    public float3 Waypoint;
}