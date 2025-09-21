using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(AIPathfindingBridgeSystem))]
public partial class EnemyMovementDiagnosticsSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities
            .WithAll<HostileNPCTag>()
            .ForEach((Entity e) =>
            {
                bool hasMove  = SystemAPI.HasComponent<NPCMovementComponent>(e);
                bool hasPath  = SystemAPI.HasComponent<NPCPathfindingComponent>(e);
                bool hasStats = SystemAPI.HasComponent<NPCBaseMovementStats>(e);
                bool hasAvoid = SystemAPI.HasComponent<AvoidanceData>(e);
                bool hasLTW   = SystemAPI.HasComponent<Unity.Transforms.LocalTransform>(e) ||
                                SystemAPI.HasComponent<Unity.Transforms.LocalToWorld>(e);
                bool hasVel   = SystemAPI.HasComponent<Unity.Physics.PhysicsVelocity>(e);
                bool hasDamp  = SystemAPI.HasComponent<Unity.Physics.PhysicsDamping>(e);

                if (hasMove && hasPath && hasStats && hasAvoid && hasLTW && hasVel && hasDamp)
                    return;

                string missing = "";
                if (!hasMove)  missing += "NPCMovementComponent ";
                if (!hasPath)  missing += "NPCPathfindingComponent ";
                if (!hasStats) missing += "NPCBaseMovementStats ";
                if (!hasAvoid) missing += "AvoidanceData ";
                if (!hasLTW)   missing += "LocalTransform/LocalToWorld ";
                if (!hasVel)   missing += "PhysicsVelocity ";
                if (!hasDamp)  missing += "PhysicsDamping ";

                //Debug.LogWarning("[EnemyDiag] " + e.Index + ": missing => " + missing.Trim());
            })
            .WithoutBurst().Run();
    }
}