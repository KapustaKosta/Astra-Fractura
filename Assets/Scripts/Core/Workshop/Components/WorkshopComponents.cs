using Energy.Core;
using Game.Production;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Game.Workshop
{
    /// <summary>
    /// Тег-маркер для сущностей цехов.
    /// </summary>
    public struct WorkshopTag : IComponentData { }
    /// <summary>
    /// Тег-маркер для сущностей станков.
    /// </summary>
    public struct StationTag : IComponentData { }

    /// <summary>
    /// Тег, добавляемый к цеху при изменении его конфигурации (добавление/удаление/перемещение станка).
    /// </summary>
    public struct WorkshopChainChangedTag : IComponentData { }

    /// <summary>
    /// Статическая конфигурация цеха.
    /// </summary>
    public struct WorkshopConfig : IComponentData
    {
        public byte Level;
        public byte SlotCount;
        public short InputCapacity;
        public short OutputCapacity;
        public short WipCapacity;
    }

    /// <summary>
    /// Динамическое состояние цеха.
    /// </summary>
    public struct WorkshopState : IComponentData
    {
        public bool IsOn;
        public int RequiredWorkers;
    }

    /// <summary>
    /// Вместимость внутреннего инвентаря (Work-in-Progress).
    /// </summary>
    public struct WIPInventoryCapacity : IComponentData { public int Value; }

    /// <summary>
    /// Элемент буфера, хранящий сущность рабочего, назначенного на цех.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct AssignedWorker : IBufferElementData { public Entity NpcEntity; }

    /// <summary>
    /// Элемент буфера, представляющий слот для станка в цехе.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct StationSlot : IBufferElementData
    {
        public Entity Station;
        public byte Order;
    }

    /// <summary>
    /// Компонент на станке, указывающий на его владельца (цех) и индекс слота.
    /// </summary>
    public struct StationOwner : IComponentData { public Entity Workshop; public int SlotIndex; }
    /// <summary>
    /// Конфигурация станка, в основном хранит ID установленного типа станка.
    /// </summary>
    public struct StationConfig : IComponentData { public int StationTypeID; }
    
    /// <summary>
    /// Перечисление возможных состояний (статусов) станка.
    /// </summary>
    public enum StationStatus : byte
    {
        Empty, Offline, AwaitingActivation, Activating, Idle, Working,
        WaitingForInput, OutputBlocked, NeedsRepair, Repairing,
        AwaitingManualLabor, ApplyingManualLabor,
        AwaitingConsumption,
        ProductionComplete,
        OutputPendingTransfer
    }

    /// <summary>
    /// Динамическое состояние станка, включая выбранный рецепт, прогресс, статус и назначения.
    /// </summary>
    public struct StationState : IComponentData
    {
        public int SelectedRecipeID;
        public float RemainingTime;
        public StationStatus Status;
        public Entity AssignedWorker;
        public Entity SpecificWorker;
        public float MaintenanceTimer;
        public byte Enabled;
        public float PowerEfficiency;
        public byte PausedNoResources;
        public int ActiveRecipeIndex;
        public float AppliedHammerCost;
        public float TimePenalty;
    }

    /// <summary>
    /// Элемент буфера для хранения готовой продукции на станке перед перемещением.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct StationOutputBufferElement : IBufferElementData { public int ItemID; public int Amount; }

    /// <summary>
    /// Элемент буфера, представляющий заказ в очереди производства цеха.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct WorkshopProductionQueueItem : IBufferElementData
    {
        public int FinalRecipeID;
        public int AmountToProduce;
        public int InitialAmount;
    }

    /// <summary>
    /// Запрос на запуск производства в цехе.
    /// </summary>
    public struct StartWorkshopProductionRequest : IComponentData
    {
        public Entity Workshop;
        public int FinalRecipeID;
        public int Amount;
        public int InitialAmount;
    }

    /// <summary>
    /// Запрос на установку типа станка в слот.
    /// </summary>
    public struct InstallStationTypeRequest : IComponentData { public Entity Workshop; public int SlotIndex; public int StationTypeID; }
    /// <summary>
    /// Запрос на установку рецепта для станка.
    /// </summary>
    public struct SetStationRecipeRequest : IComponentData { public Entity Workshop; public int SlotIndex; public int RecipeID; }
    /// <summary>
    /// Запрос на удаление (очистку) станка из слота.
    /// </summary>
    public struct RemoveStationRequest : IComponentData { public Entity Workshop; public int SlotIndex; }
    /// <summary>
    /// Запрос на перемещение станка из одного слота в другой.
    /// </summary>
    public struct MoveStationRequest : IComponentData { public Entity Workshop; public int FromIndex; public int ToIndex; }

    /// <summary>
    /// Запрос на включение/выключение всего цеха.
    /// </summary>
    public struct ToggleWorkshopRequest : IComponentData
    {
        public Entity Workshop;
        public bool Enable;
    }

    /// <summary>
    /// Запрос на включение/выключение отдельного станка.
    /// </summary>
    public struct ToggleStationRequest : IComponentData
    {
        public Entity Workshop;
        public int SlotIndex;
        public bool Enable;
    }
    
    /// <summary>
    /// Запрос на назначение конкретного рабочего на станок.
    /// </summary>
    public struct AssignWorkerToStationRequest : IComponentData
    {
        public Entity Workshop;
        public int SlotIndex;
        public Entity NpcEntity;
    }

    /// <summary>
    /// Запрос на снятие специфического назначения рабочего со станка.
    /// </summary>
    public struct UnassignWorkerFromStationRequest : IComponentData
    {
        public Entity Workshop;
        public int SlotIndex;
    }

    /// <summary>
    /// Запрос на полное снятие рабочего с цеха.
    /// </summary>
    public struct UnassignWorkerFromWorkshopRequest : IComponentData
    {
        public Entity Workshop;
        public Entity NpcEntity;
    }
}

namespace Game.Workshop
{
    /// <summary>
    /// Компонент, управляющий процессом технического обслуживания цеха.
    /// </summary>
    public struct WorkshopMaintenanceInProgress : IComponentData
    {
        public Entity WorkshopEntity;
        public float TimePerStation;
        public float CurrentStationTimer;
        public int NextStationSlotIndex;
    }

    /// <summary>
    /// Элемент буфера для внутреннего инвентаря цеха (Work-in-Progress).
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct WorkshopWIPBufferElement : IBufferElementData
    {
        public int ItemID;
        public int Amount;
    }
    /// <summary>
    /// Тег, указывающий, что цех находится в процессе технического обслуживания.
    /// </summary>
    public struct WorkshopUnderMaintenanceTag : IComponentData { }
}