using Unity.Entities;
using Game.Production;
using UnityEngine;

namespace Conveyor
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class RouteTransferSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
            float dt = SystemAPI.Time.DeltaTime;
            
            var generalInvLookup = GetBufferLookup<InventoryItemElement>(false);
            var outputInvLookup = GetBufferLookup<OutputInventorySlot>(false);
            var segmentSettingsLookup = GetComponentLookup<ConveyorSegmentSettings>(true);
            var connectorLookup = GetComponentLookup<ConveyorConnector>(true);
            var powerScalingLookup = GetComponentLookup<RoutePowerScaling>(true);

            Entities
                .WithAll<ActiveRouteTag>()
                .WithReadOnly(segmentSettingsLookup)
                .WithReadOnly(connectorLookup)
                .WithReadOnly(powerScalingLookup)
                .ForEach((Entity routeEntity, ref RouteTimer timer, in RouteDefinition routeDef, in DynamicBuffer<RoutePathElement> path) =>
                {
                    float speedMultiplier = powerScalingLookup.TryGetComponent(routeEntity, out var scaling) 
                        ? scaling.SpeedMultiplier 
                        : 1.0f;

                    if (speedMultiplier < 0.001f) return;

                    timer.TimeToNextTransfer -= dt;
                    if (timer.TimeToNextTransfer > 0) return;

                    int batchSize = routeDef.TransferBatchSize > 0 ? routeDef.TransferBatchSize : 1;
                    float throughput = routeDef.ThroughputPerMinute > 0 ? routeDef.ThroughputPerMinute : 120;
                    float batchesPerMinute = throughput / batchSize;
                    float baseCooldown = 60.0f / batchesPerMinute;
                    
                    timer.TimeToNextTransfer = baseCooldown;

                    if (!connectorLookup.HasComponent(routeDef.StartConnector) || !connectorLookup.HasComponent(routeDef.EndConnector)) return;

                    var sourceConnectorOwner = connectorLookup[routeDef.StartConnector].Owner;
                    var destConnectorOwner = connectorLookup[routeDef.EndConnector].Owner;
                    int amountToTake = batchSize;
                    bool itemTaken = false;

                    if (outputInvLookup.HasBuffer(sourceConnectorOwner))
                    {
                        var buffer = outputInvLookup[sourceConnectorOwner].Reinterpret<InventoryItemElement>();
                        for (int i = 0; i < buffer.Length; i++) {
                            if (buffer[i].ItemID == routeDef.ItemID && buffer[i].Amount >= amountToTake) {
                                var item = buffer[i]; item.Amount -= amountToTake;
                                buffer[i] = item.Amount > 0 ? item : default;
                                itemTaken = true; ecb.AddComponent<InventoryChangedTag>(sourceConnectorOwner); break;
                            }
                        }
                    }
                    if (!itemTaken && generalInvLookup.HasBuffer(sourceConnectorOwner))
                    {
                        var buffer = generalInvLookup[sourceConnectorOwner];
                        for (int i = 0; i < buffer.Length; i++) {
                            if (buffer[i].ItemID == routeDef.ItemID && buffer[i].Amount >= amountToTake) {
                                var item = buffer[i]; item.Amount -= amountToTake;
                                buffer[i] = item.Amount > 0 ? item : default;
                                itemTaken = true; ecb.AddComponent<InventoryChangedTag>(sourceConnectorOwner); break;
                            }
                        }
                    }

                    if (itemTaken)
                    {
                        float totalLength = 0;
                        foreach (var segment in path) {
                            if (segmentSettingsLookup.HasComponent(segment.SegmentEntity))
                                totalLength += segmentSettingsLookup[segment.SegmentEntity].Length;
                        }
                        float speed = 2.0f;
                        var conveyorPrefab = ItemToEntityResolver.GetEntityPrefabFromID(EntityManager, routeDef.ItemID);
                        if (conveyorPrefab != Entity.Null && SystemAPI.HasComponent<ConveyorSegmentSettings>(conveyorPrefab))
                            speed = SystemAPI.GetComponent<ConveyorSegmentSettings>(conveyorPrefab).Speed;

                        float travelTime = (speed > 0.01f) ? totalLength / speed : float.MaxValue;
                        var transitEntity = ecb.CreateEntity();
                        ecb.AddComponent(transitEntity, new ItemInTransit {
                            RouteEntity = routeEntity, ItemID = routeDef.ItemID, Amount = amountToTake,
                            DestinationInventory = destConnectorOwner, CurrentTravelTime = 0f,
                            TravelDuration = travelTime > 0.1f ? travelTime : 0.1f
                        });
                    }
                }).WithoutBurst().Run();
        }
    }
}