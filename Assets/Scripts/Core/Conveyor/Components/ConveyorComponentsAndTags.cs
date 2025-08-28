using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace Conveyor
{
    public struct InConveyorMode : IComponentData { }

    public struct ConveyorState : IComponentData
    {
        public Entity PreviewEntity;
        public bool HasStart;
        public Entity StartConnector;
        public int SegmentsLocked;
        public float SnapRadius;
        public int ItemID;
    }

    public struct BuildingName : IComponentData
    {
        public FixedString64Bytes Value;
    }

    public enum ConveyorConnectorType : byte { In = 0, Out = 1, Bidirectional = 2 }

    public struct ConveyorConnector : IComponentData
    {
        public ConveyorConnectorType Type;
        public Entity Owner;
        public float3 LocalPosition;
        public Entity ConnectedSegment;
    }

    public struct ConveyorConnectorHighlighted : IComponentData { }
    public struct HoveredConnectorTag : IComponentData { }

    public struct ConveyorSegmentSettings : IComponentData
    {
        public float Length;
        public float MinLength;
        public float MaxLength;
        public float ItemsPerMinute;
        public float Speed;
    }

    public struct ConveyorPreviewTag : IComponentData { }
    public struct ConveyorGhostTag : IComponentData { }
    public struct ConveyorOccupiedTag : IComponentData { }

    public struct ConveyorPreviewRuntime : IComponentData
    {
        public int LastTailCount;
        public float LastTailPerLen;
    }

    [InternalBufferCapacity(32)] public struct ConveyorPathPoint : IBufferElementData { public float3 Position; public byte IsLocked; }
    [InternalBufferCapacity(32)] public struct ConveyorWaypoint : IBufferElementData { public float3 Position; }
    [InternalBufferCapacity(32)] public struct ConveyorBuildPathPoint : IBufferElementData { public float3 Position; }

    [InternalBufferCapacity(32)] public struct ConveyorFrozenPose : IBufferElementData { public float3 Position; public quaternion Rotation; public float Length; }
    [InternalBufferCapacity(32)] public struct ConveyorLivePose : IBufferElementData { public float3 Position; public quaternion Rotation; public float Length; }

    [InternalBufferCapacity(16)] public struct ConveyorGhostFrozenRef : IBufferElementData { public Entity Value; }
    [InternalBufferCapacity(16)] public struct ConveyorGhostLiveRef : IBufferElementData { public Entity Value; }

    public struct EnterConveyorModeRequest : IComponentData { public int ItemID; }
    public struct ExitConveyorModeRequest : IComponentData { }

    public struct ConfirmConveyorPlacementRequest : IComponentData { public int ItemID; public Entity PreviewHolder; public Entity StartConnector; public Entity EndConnector; }
    public struct ConveyorBuildFromPathRequest : IComponentData { public Entity StartConnector; public Entity EndConnector; public int ItemID; public Entity PreviewHolder; }
    public struct PostBuildConnectorUpdateRequest : IComponentData { public Entity StartConnector; public Entity EndConnector; }
    [InternalBufferCapacity(64)] public struct NewlyBuiltConveyorSegmentRef : IBufferElementData { public Entity Value; }

    public struct RecalculateRoutesForNetworkRequest : IComponentData
    {
        public Entity SourceBuilding;
    }

    public struct ConveyorSegmentScale : IComponentData
    {
        public float Z; // масштаб по оси Z (отношение фактической длины к базовой)
    }

    public struct ConveyorPlacementValidTag : IComponentData { }

    /// <summary>
    /// Фактическая длина конкретного экземпляра сегмента конвейера (после масштабирования).
    /// Записывается при строительстве и используется визуализацией/таймингами.
    /// </summary>
    public struct ConveyorSegmentRuntimeLength : IComponentData
    {
        public float Value;
    }
}

