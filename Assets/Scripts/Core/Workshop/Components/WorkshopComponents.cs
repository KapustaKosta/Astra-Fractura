using Unity.Entities;
using Unity.Collections;
using Energy.Core;
using Game.Production;

namespace Game.Workshop
{
    public struct WorkshopTag : IComponentData { }
    public struct StationTag : IComponentData { }

    public struct WorkshopChainChangedTag : IComponentData { }

    public struct WorkshopConfig : IComponentData
    {
        public byte Level;
        public byte SlotCount;
        public short InputCapacity;
        public short OutputCapacity;
        public short WipCapacity;
    }

    public struct WorkshopState : IComponentData
    {
        public bool IsOn;
        public int RequiredWorkers;
    }

    public struct WIPInventoryCapacity : IComponentData { public int Value; }

    [InternalBufferCapacity(8)]
    public struct AssignedWorker : IBufferElementData { public Entity NpcEntity; }

    [InternalBufferCapacity(16)]
    public struct StationSlot : IBufferElementData
    {
        public Entity Station;
        public byte Order;
    }

    public struct StationOwner : IComponentData { public Entity Workshop; public int SlotIndex; }
    public struct StationConfig : IComponentData { public int StationTypeID; }

    public enum StationStatus : byte
    {
        Empty, Offline, AwaitingActivation, Activating, Idle, Working,
        WaitingForInput, OutputBlocked, NeedsRepair, Repairing,
        AwaitingManualLabor, ApplyingManualLabor
    }

    public struct StationState : IComponentData
    {
        public int SelectedRecipeID;
        public float RemainingTime;
        public StationStatus Status;
        public Entity AssignedWorker;
        public float MaintenanceTimer;
        public byte Enabled;
        public byte PausedNoPower;
        public byte PausedNoResources;
        public int ActiveRecipeIndex;
        public float AppliedHammerCost;
        public float TimePenalty;
    }

    [InternalBufferCapacity(8)]
    public struct StationOutputBufferElement : IBufferElementData { public int ItemID; public int Amount; }

    [InternalBufferCapacity(16)]
    public struct WorkshopProductionQueueItem : IBufferElementData
    {
        public int FinalRecipeID;
        public int AmountToProduce;
        public int InitialAmount;
    }

    public struct StartWorkshopProductionRequest : IComponentData
    {
        public Entity Workshop;
        public int FinalRecipeID;
        public int Amount;
        public int InitialAmount;
    }


    public struct InstallStationTypeRequest : IComponentData { public Entity Workshop; public int SlotIndex; public int StationTypeID; }
    public struct SetStationRecipeRequest : IComponentData { public Entity Workshop; public int SlotIndex; public int RecipeID; }
    public struct RemoveStationRequest : IComponentData { public Entity Workshop; public int SlotIndex; }
    public struct MoveStationRequest : IComponentData { public Entity Workshop; public int FromIndex; public int ToIndex; }

    public struct ToggleWorkshopRequest : IComponentData
    {
        public Entity Workshop;
        public bool Enable;
    }

    public struct ToggleStationRequest : IComponentData
    {
        public Entity Workshop;
        public int SlotIndex;
        public bool Enable;
    }
}

namespace Game.Workshop
{
    public struct WorkshopMaintenanceInProgress : IComponentData
    {
        public Entity WorkshopEntity;
        public float TimePerStation;
        public float CurrentStationTimer;
        public int NextStationSlotIndex;
    }

    [InternalBufferCapacity(32)]
    public struct WorkshopWIPBufferElement : IBufferElementData
    {
        public int ItemID;
        public int Amount;
    }
    public struct WorkshopUnderMaintenanceTag : IComponentData { }

}

