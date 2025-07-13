using Unity.Entities;
using Unity.Transforms;

/// <summary>
/// Система, обрабатывающая действия, связанные с главным поселением игрока,
/// такие как найм NPC и назначение им задач.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SettlementActionsSystem : SystemBase
{
    /// <summary>
    /// Гарантирует, что система будет активна, только когда в мире появится главное поселение игрока.
    /// </summary>
    protected override void OnCreate()
    {
        RequireForUpdate<PlayerSettlementTag>(); 
    }

    /// <summary>
    /// Вызывается каждый кадр для обработки запросов найма и назначения задач.
    /// </summary>
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        
        var playerSettlementEntity = SystemAPI.GetSingletonEntity<PlayerSettlementTag>();
        var settlementData = SystemAPI.GetComponentRW<SettlementComponent>(playerSettlementEntity);

        Entities
            .WithoutBurst()
            .ForEach((in HireNPCRequest request) =>
            {
                if (!SystemAPI.Exists(request.NPCToHire)) return;

                if (settlementData.ValueRO.NPCs.Length < settlementData.ValueRO.NPCs.Capacity && !SystemAPI.HasComponent<NPCHiredTag>(request.NPCToHire))
                {
                    settlementData.ValueRW.NPCs.Add(request.NPCToHire);
                    settlementData.ValueRW.Population += 1;
                    ecb.AddComponent<NPCHiredTag>(request.NPCToHire);
                }
            }).Run();

        Entities
            .ForEach((in AssignNPCToTaskRequest request) =>
            {
                if (!SystemAPI.Exists(request.NPC) || !SystemAPI.Exists(request.TargetResourceNode)) return;

                if (SystemAPI.HasComponent<NPCComponent>(request.NPC) && SystemAPI.HasComponent<NPCMovementComponent>(request.NPC))
                {
                    var npcData = SystemAPI.GetComponentRW<NPCComponent>(request.NPC);
                    npcData.ValueRW.Target = request.TargetResourceNode;

                    var movementData = SystemAPI.GetComponentRW<NPCMovementComponent>(request.NPC);
                    var targetTransform = SystemAPI.GetComponent<LocalTransform>(request.TargetResourceNode);
                    movementData.ValueRW.TargetPosition = targetTransform.Position;
                    movementData.ValueRW.HasTarget = true;
                }
            }).Schedule();
    }
}