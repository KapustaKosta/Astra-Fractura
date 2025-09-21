using Unity.Entities;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(AIPathfindingBridgeSystem))] 
public partial class StopAttackingNPCSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities
            .WithAll<IsAttackingTag>()
            .ForEach((ref NPCMovementComponent movement) =>
            {
                if (movement.HasTarget)
                {
                    movement.HasTarget = false;
                }
            })
            .ScheduleParallel();
    }
}