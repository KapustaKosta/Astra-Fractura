using Unity.Entities;

namespace Game.Production
{
    public struct ProductionBuildingTag : IComponentData { }
    public struct HasInputInventory : IComponentData { }
    public struct HasOutputInventory : IComponentData { }

    public enum ProductionStatus : byte
    {
        Idle,
        Working,
        PausedNoPower,
        PausedNoResources,
        AwaitingManualLabor
    }

    public struct InputInventorySlot : IBufferElementData
    {
        public int ItemID;
        public int Amount;
    }

    public struct OutputInventorySlot : IBufferElementData
    {
        public int ItemID;
        public int Amount;
    }

    public struct ProductionConfig : IComponentData
    {
        public int StationTypeID;
    }

    public struct ProductionBuildingState : IComponentData
    {
        public bool IsOn;
        public int SelectedRecipeID;
        public int ActiveRecipeIndex; // Индекс в BlobAsset
        public float RemainingTime;
        public ProductionStatus Status;
        public float AppliedHammerCost;
        public Entity AssignedWorker; 
    }

    public struct ProductionQueueItem : IBufferElementData
    {
        public int RecipeID;
        public int AmountToProduce;
    }

    public struct StartProductionRequest : IComponentData
    {
        public Entity Building;
        public int RecipeID;
        public int Amount;
    }

    public struct StopProductionRequest : IComponentData
    {
        public Entity Building;
    }

    public struct SetProductionRecipeRequest : IComponentData
    {
        public Entity Building;
        public int RecipeID;
    }
}