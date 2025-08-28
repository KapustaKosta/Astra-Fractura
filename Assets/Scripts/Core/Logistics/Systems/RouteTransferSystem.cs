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
            float currentTime = (float)SystemAPI.Time.ElapsedTime;

            var generalInvLookup = GetBufferLookup<InventoryItemElement>(false);
            var outputInvLookup = GetBufferLookup<OutputInventorySlot>(false);
            var segmentSettingsLookup = GetComponentLookup<ConveyorSegmentSettings>(true);
            var connectorLookup = GetComponentLookup<ConveyorConnector>(true);

            Entities
                .WithAll<ActiveRouteTag>()
                .WithReadOnly(segmentSettingsLookup)
                .WithReadOnly(connectorLookup)
                .ForEach((Entity routeEntity, ref RouteTimer timer, in RouteDefinition routeDef, in DynamicBuffer<RoutePathElement> path) =>
                {
                    timer.TimeToNextTransfer -= dt;
                    if (timer.TimeToNextTransfer > 0) return;

                    // Динамически рассчитываем задержку на основе пропускной способности и размера стака
                    int batchSize = routeDef.TransferBatchSize > 0 ? routeDef.TransferBatchSize : 1;
                    float throughput = routeDef.ThroughputPerMinute > 0 ? routeDef.ThroughputPerMinute : 120;
                    float batchesPerMinute = throughput / batchSize;
                    float cooldown = 60.0f / batchesPerMinute;
                    timer.TimeToNextTransfer = cooldown;

                    if (!connectorLookup.HasComponent(routeDef.StartConnector) || !connectorLookup.HasComponent(routeDef.EndConnector)) return;

                    var sourceConnectorOwner = connectorLookup[routeDef.StartConnector].Owner;
                    var destConnectorOwner = connectorLookup[routeDef.EndConnector].Owner;

                    int amountToTake = batchSize;
                    bool itemTaken = false;

                    // Проверяем наличие целого стака
                    if (outputInvLookup.HasBuffer(sourceConnectorOwner))
                    {
                        var buffer = outputInvLookup[sourceConnectorOwner].Reinterpret<InventoryItemElement>();
                        for (int i = 0; i < buffer.Length; i++)
                        {
                            if (buffer[i].ItemID == routeDef.ItemID && buffer[i].Amount >= amountToTake)
                            {
                                var item = buffer[i];
                                item.Amount -= amountToTake;
                                buffer[i] = item.Amount > 0 ? item : default;

                                itemTaken = true;
                                ecb.AddComponent<InventoryChangedTag>(sourceConnectorOwner);
                                break;
                            }
                        }
                    }

                    if (!itemTaken && generalInvLookup.HasBuffer(sourceConnectorOwner))
                    {
                        var buffer = generalInvLookup[sourceConnectorOwner];
                        for (int i = 0; i < buffer.Length; i++)
                        {
                            if (buffer[i].ItemID == routeDef.ItemID && buffer[i].Amount >= amountToTake)
                            {
                                var item = buffer[i];
                                item.Amount -= amountToTake;
                                buffer[i] = item.Amount > 0 ? item : default;

                                itemTaken = true;
                                ecb.AddComponent<InventoryChangedTag>(sourceConnectorOwner);
                                break;
                            }
                        }
                    }

                    if (itemTaken)
                    {
                        float totalLength = 0;
                        foreach (var segment in path)
                        {
                            if (segmentSettingsLookup.HasComponent(segment.SegmentEntity))
                            {
                                totalLength += segmentSettingsLookup[segment.SegmentEntity].Length;
                            }
                        }

                        float speed = 2.0f;
                        var conveyorPrefab = ItemToEntityResolver.GetEntityPrefabFromID(EntityManager, routeDef.ItemID);
                        if (conveyorPrefab != Entity.Null && SystemAPI.HasComponent<ConveyorSegmentSettings>(conveyorPrefab))
                        {
                            speed = SystemAPI.GetComponent<ConveyorSegmentSettings>(conveyorPrefab).Speed;
                        }

                        float travelTime = totalLength / speed;

                        var transitEntity = ecb.CreateEntity();
                        ecb.AddComponent(transitEntity, new ItemInTransit
                        {
                            RouteEntity = routeEntity,
                            ItemID = routeDef.ItemID,
                            Amount = amountToTake, // Отправляем весь стак
                            DestinationInventory = destConnectorOwner,
                            StartTime = currentTime,
                            TravelDuration = travelTime > 0.1f ? travelTime : 0.1f
                        });
                    }

                }).WithoutBurst().Run();
        }
    }
}