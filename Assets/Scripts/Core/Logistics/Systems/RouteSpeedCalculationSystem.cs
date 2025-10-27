using Unity.Entities;
using Unity.Mathematics;

namespace Conveyor
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ConveyorPowerFeedbackSystem))]
    [UpdateAfter(typeof(RouteStateManagementSystem))]
    [UpdateBefore(typeof(RouteTransferSystem))]
    public partial class RouteSpeedCalculationSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);

            Entities
                .ForEach((Entity e, in RoutePowerStatus power) =>
                {
                    float finalMultiplier = SystemAPI.HasComponent<ActiveRouteTag>(e)
                        ? math.saturate(power.PowerRatio)
                        : 0f;

                    var scaling = new RoutePowerScaling { SpeedMultiplier = finalMultiplier };

                    if (SystemAPI.HasComponent<RoutePowerScaling>(e))
                        ecb.SetComponent(e, scaling);
                    else
                        ecb.AddComponent(e, scaling);
                    
                }).Run();
        }
    }
}