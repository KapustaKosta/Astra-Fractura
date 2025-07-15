using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Интерфейс-маркер для всех компонентов-запросов,
/// которые должны быть удалены в конце кадра.
/// </summary>
public interface IRequestCleanup : IComponentData { }

/// <summary>
/// Запрос на вход в режим строительства для определенного предмета.
/// </summary>
public struct EnterBuildingModeRequest : IRequestCleanup { public int ItemID; }

/// <summary>
/// Запрос на выход из режима строительства.
/// </summary>
public struct ExitBuildingModeRequest : IRequestCleanup { }

/// <summary>
/// Запрос на размещение здания в указанной точке мира с определенным поворотом.
/// Потребляет указанный предмет из инвентаря.
/// </summary>
public struct PlaceBuildingRequest : IRequestCleanup
{
    public float3 Position;
    public quaternion Rotation;
    public Entity BuildingPrefabToPlace;
    public int ItemIDToConsume;
}

/// <summary>
/// Запрос на переключение состояния инвентаря (открыть/закрыть).
/// </summary>
public struct ToggleInventoryRequest : IRequestCleanup { }

/// <summary>
/// Запрос на открытие пользовательского интерфейса для взаимодействия с NPC.
/// </summary>
public struct OpenNPCUIRequest : IRequestCleanup { public Entity Target; }

/// <summary>
/// Запрос на открытие пользовательского интерфейса поселения.
/// </summary>
public struct OpenSettlementUIRequest : IRequestCleanup { public Entity Target; }

/// <summary>
/// Запрос на открытие пользовательского интерфейса для торговли с сущностью.
/// </summary>
public struct OpenTradeUIRequest : IRequestCleanup { public Entity Target; }

/// <summary>
/// Запрос на закрытие всех открытых окон пользовательского интерфейса.
/// </summary>
public struct CloseAllUIRequest : IRequestCleanup { }

/// <summary>
/// Запрос на выполнение взаимодействия с целевым объектом в мире.
/// </summary>
public struct InteractionRequest : IRequestCleanup { }

/// <summary>
/// Запрос на найм указанного NPC.
/// </summary>
public struct HireNPCRequest : IRequestCleanup { public Entity NPCToHire; }


/// <summary>
/// Запрос на проверку возможности сбора ресурса.
/// </summary>
public struct ValidateHarvestAttemptRequest : IRequestCleanup
{
    public Entity Harvester;
    public Entity TargetResourceNode;
}


/// <summary>
/// Запрос на добавление указанного количества предмета в инвентарь целевой сущности.
/// </summary>
public struct AddItemRequest : IRequestCleanup
{
    public Entity TargetInventoryOwner;
    public int ItemID;
    public int Amount;
}

/// <summary>
/// Запрос на удаление указанного количества предмета из инвентаря целевой сущности.
/// </summary>
public struct RemoveItemRequest : IRequestCleanup
{
    public Entity TargetInventoryOwner;
    public int ItemID;
    public int Amount;
}

/// <summary>
/// Запрос на перемещение предмета из одного слота инвентаря в другой.
/// Может использоваться для перемещения между разными инвентарями.
/// </summary>
public struct MoveItemRequest : IRequestCleanup
{
    public Entity SourceInventoryOwner;
    public int SourceSlotIndex;
    public Entity DestinationInventoryOwner;
    public int DestinationSlotIndex;
    public int ItemID;
    public int Amount;
}

/// <summary>
/// Запрос на разделение стака предмета. Перемещает указанное количество
/// из исходного слота в целевой.
/// </summary>
public struct SplitStackRequest : IRequestCleanup
{
    public Entity SourceInventoryOwner;
    public int SourceSlotIndex;
    public Entity DestinationInventoryOwner;
    public int DestinationSlotIndex;
    public int AmountToMove;
}


/// <summary>
/// Запрос на отображение короткого текстового сообщения в UI.
/// </summary>
public struct UINotificationRequest : IComponentData
{
    public FixedString128Bytes Message;
}

/// <summary>
/// Одноразовый запрос на атомарную передачу предметов из одного инвентаря в другой.
/// </summary>
public struct TransferItemRequest : IComponentData
{
    public Entity FromInventoryOwner;
    public Entity ToInventoryOwner;
}


