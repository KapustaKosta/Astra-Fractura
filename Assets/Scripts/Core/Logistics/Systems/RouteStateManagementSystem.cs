using Unity.Entities;
using UnityEngine;

namespace Conveyor
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ConveyorRoutesUISystem))] 
    [UpdateBefore(typeof(RouteSpeedCalculationSystem))]
    public partial class RouteStateManagementSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);

            Entities
                .WithAll<UserEnabledRouteTag>()
                .WithNone<ActiveRouteTag>()
                .ForEach((Entity e, in RouteDefinition routeDef) =>
                {
                    if (routeDef.ItemID > 0)
                    {
                        ecb.AddComponent<ActiveRouteTag>(e);
                    }
                }).Run();

            Entities
                .WithAll<ActiveRouteTag>()
                .ForEach((Entity e, in RouteDefinition routeDef) =>
                {
                    bool userWantsItOn = SystemAPI.HasComponent<UserEnabledRouteTag>(e);

                    if (!userWantsItOn || routeDef.ItemID <= 0)
                    {
                        ecb.RemoveComponent<ActiveRouteTag>(e);
                    }
                }).Run();
        }
    }
}