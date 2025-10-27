using Unity.Entities;
using Unity.Mathematics;

namespace Conveyor
{
    public struct RouteDefinition : IComponentData
    {
        public Entity StartConnector;
        public Entity EndConnector;
        public int ItemID;
        public float ThroughputPerMinute;
        public int TransferBatchSize;
    }

    [InternalBufferCapacity(128)]
    public struct RoutePathElement : IBufferElementData
    {
        public Entity SegmentEntity;
    }

    public struct BelongsToRoute : IComponentData
    {
        public Entity RouteEntity;
    }

    public struct ConveyorLink : IComponentData
    {
        public Entity NextSegment;
    }

    public struct RouteTimer : IComponentData
    {
        public float Cooldown;
        public float TimeToNextTransfer;
    }

    public struct ActiveRouteTag : IComponentData { }

    public struct ItemInTransit : IComponentData
    {
        public Entity RouteEntity;
        public int ItemID;
        public int Amount;
        public Entity DestinationInventory;
        public float CurrentTravelTime;
        public float TravelDuration;
    }

    public struct ItemVisualTag : IComponentData { }

    public struct HasVisualTag : IComponentData { }



    public struct VisualFor : IComponentData
    {
        public Entity LogicalEntity;
    }

    public struct DiscoverRouteRequest : IComponentData
    {
        public Entity StartConnector;
        public Entity EndConnector;
    }

    public struct AwaitingDiscoveryTag : IComponentData { }

    public struct OpenConveyorRoutesUIRequest : IComponentData { }

    public struct SetRouteItemRequest : IComponentData
    {
        public Entity RouteEntity;
        public int NewItemID;
    }

    public struct ToggleRouteRequest : IComponentData
    {
        public Entity RouteEntity;
    }
    
    [InternalBufferCapacity(128)]
    public struct RouteJoint : IBufferElementData
    {
        public float3 Position;
    }
    
    public struct NeedsEnergySetupTag : IComponentData { }
    
    public struct RouteNetworkInfo : IComponentData
    {
        public int StartSubnetId;
        public int EndSubnetId;
    }
}