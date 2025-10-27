using Unity.Entities;
using Energy.Core;
using Unity.Collections;

namespace Conveyor
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(RouteRecalculationSystem))]
    [UpdateAfter(typeof(NetworkDiscoverySystem))]
    public partial class RouteEnergySetupSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var connectorLookup = GetComponentLookup<ConveyorConnector>(true);
            var energySettingsLookup = GetComponentLookup<ConveyorEnergySettings>(true);
            var networkNodeLookup = GetComponentLookup<NetworkNode>(true);

            foreach (var (routeDef, path, entity) in SystemAPI.Query<RefRO<RouteDefinition>, DynamicBuffer<RoutePathElement>>()
                .WithAll<NeedsEnergySetupTag>().WithEntityAccess())
            {
                var prefab = ItemToEntityResolver.GetEntityPrefabFromID(EntityManager, routeDef.ValueRO.ItemID);
                float demandPerSegment = 0.1f;
                if (prefab != Entity.Null && energySettingsLookup.HasComponent(prefab))
                {
                    demandPerSegment = energySettingsLookup[prefab].Value;
                }
                float totalDemand = demandPerSegment * path.Length;
                ecb.AddComponent(entity, new ConveyorEnergyDemand { RequiredKW = totalDemand });

                int startNetId = 0;
                int endNetId = 0;

                if (connectorLookup.HasComponent(routeDef.ValueRO.StartConnector))
                {
                    var startOwner = connectorLookup[routeDef.ValueRO.StartConnector].Owner;
                    if (networkNodeLookup.HasComponent(startOwner))
                    {
                        startNetId = networkNodeLookup[startOwner].SubnetId;
                    }
                }
                
                if (connectorLookup.HasComponent(routeDef.ValueRO.EndConnector))
                {
                    var endOwner = connectorLookup[routeDef.ValueRO.EndConnector].Owner;
                    if (networkNodeLookup.HasComponent(endOwner))
                    {
                        endNetId = networkNodeLookup[endOwner].SubnetId;
                    }
                }

                ecb.AddComponent(entity, new RouteNetworkInfo { StartSubnetId = startNetId, EndSubnetId = endNetId });
                ecb.SetComponent(entity, new NetworkNode { SubnetId = startNetId });
                ecb.RemoveComponent<NeedsEnergySetupTag>(entity);
            }
            
            ecb.Playback(EntityManager);
        }
    }
}