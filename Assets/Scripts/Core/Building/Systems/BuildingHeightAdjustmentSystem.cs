using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(RegularBuildingPreviewPlacementSystem))]
public partial class BuildingHeightAdjustmentSystem : SystemBase
{
    private const float HEIGHT_STEP = 0.25f;
    private const float MAX_HEIGHT = 50.0f;

    protected override void OnUpdate()
    {
        if (!SystemAPI.TryGetSingletonRW<BuildingHeightOffset>(out var heightOffsetRW))
            return;

        var ecb = SystemAPI
            .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

        // применяем все запросы скролла (их создаёт только FoundationScrollInputSystem)
        foreach (var (request, reqEntity) in SystemAPI.Query<RefRO<AdjustBuildingHeightRequest>>().WithEntityAccess())
        {
            float next = math.clamp(heightOffsetRW.ValueRO.Value + request.ValueRO.ScrollDelta * HEIGHT_STEP, 0f, MAX_HEIGHT);
            heightOffsetRW.ValueRW.Value = next;
            ecb.DestroyEntity(reqEntity);
        }
    }
}
