// Assets/Scripts/Core/Combat/Systems/ClearDropRequestedFlagSystem.cs
using Unity.Entities;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(InitDroppedVisualsSystem))]
public partial class ClearDropRequestedFlagSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = SystemAPI
            .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

        // Снимаем DropRequested только у тех трупов, где больше нет PendingDroppedVisualInit на сцене
        // (то есть все визуалы уже инициализированы)
        Entities
            .WithAll<IsDeadTag, DropRequested>()
            .WithNone<Disabled>()
            .ForEach((Entity e) =>
            {
                ecb.RemoveComponent<DropRequested>(e);
            })
            .Schedule();
    }
}