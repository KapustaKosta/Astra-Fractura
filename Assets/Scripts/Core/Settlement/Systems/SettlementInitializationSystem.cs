using Unity.Entities;

/// <summary>
/// Система, отвечающая за инициализацию только что построенных зданий.
/// В частности, она назначает первое построенное поселение-кандидат
/// главным поселением игрока.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SettlementInitializationSystem : SystemBase
{
    private EntityQuery playerSettlementQuery;

    /// <summary>
    /// Вызывается при создании системы для инициализации запроса.
    /// </summary>
    protected override void OnCreate()
    {
        playerSettlementQuery = GetEntityQuery(typeof(PlayerSettlementTag));
    }

    /// <summary>
    /// Вызывается каждый кадр для проверки и назначения главного поселения.
    /// </summary>
    protected override void OnUpdate()
    {
        if (!playerSettlementQuery.IsEmpty)
        {
            this.Enabled = false;
            return;
        }

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        bool isPlayerSettlementMissing = playerSettlementQuery.IsEmpty;

        Entities
            .WithAll<NewlyBuiltTag, PlayerSettlementCandidateTag>()
            .ForEach((Entity newBuildingEntity, in PlayerSettlementCandidateTag candidate) =>
            {
                if (isPlayerSettlementMissing)
                {
                    ecb.AddComponent<PlayerSettlementTag>(newBuildingEntity);
                    isPlayerSettlementMissing = false;
                }
                
                ecb.RemoveComponent<NewlyBuiltTag>(newBuildingEntity);

            }).Schedule();
    }
}