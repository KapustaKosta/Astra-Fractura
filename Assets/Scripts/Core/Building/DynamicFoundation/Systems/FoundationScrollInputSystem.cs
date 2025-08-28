using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(BuildingHeightAdjustmentSystem))]
public partial class FoundationScrollInputSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<BuildingPreviewTag>();
        RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
    }

    protected override void OnUpdate()
    {
        // Скроллим только если превью существует И это фундамент.
        if (!SystemAPI.TryGetSingletonEntity<BuildingPreviewTag>(out var preview))
            return;
        if (!SystemAPI.HasComponent<FoundationTag>(preview))
            return;

        float scroll = Input.mouseScrollDelta.y;
        if (scroll == 0f) return;

        var ecb = SystemAPI
            .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

        var req = ecb.CreateEntity();
        ecb.AddComponent(req, new AdjustBuildingHeightRequest { ScrollDelta = scroll });
    }
}
