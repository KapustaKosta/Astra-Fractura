using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Game.Workshop;
using Wiring;
using Conveyor;
using Energy.Core;
using Game.Research;

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
/// Запрос на открытие пользовательского интерфейса производственного здания.
/// </summary>
public struct OpenProductionUIRequest : IRequestCleanup { public Entity Target; }

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

/// <summary>
/// Запрос на открытие пользовательского интерфейса цеха (Workshop).
/// </summary>
public struct OpenWorkshopUIRequest : IRequestCleanup { public Entity Target; }

/// <summary>
/// Запрос на вход в режим прокладки проводов.
/// </summary>
public struct EnterWirePlacementModeRequest : IRequestCleanup { public int WireLevel; }

/// <summary>
/// Запрос на выход из режима прокладки проводов.
/// </summary>
public struct ExitWirePlacementModeRequest : IRequestCleanup { }

/// <summary>
/// Запрос на вход в режим строительства конвейеров.
/// </summary>
public struct EnterConveyorModeRequest : IRequestCleanup { public int ItemID; }

/// <summary>
/// Запрос на выход из режима строительства конвейеров.
/// </summary>
public struct ExitConveyorModeRequest : IRequestCleanup { }

/// <summary>
/// Запрос на открытие UI генератора.
/// </summary>
public struct OpenGeneratorUIRequest : IRequestCleanup { public Entity Target; }

/// <summary>
/// Запрос на открытие UI батареи.
/// </summary>
public struct OpenBatteryUIRequest : IRequestCleanup { public Entity Target; }

/// <summary>
/// Запрос на включение/выключение генератора.
/// </summary>
public struct ToggleGeneratorRequest : IRequestCleanup { public Entity Target; public bool DesiredOn; }

/// <summary>
/// Запрос на установку предмета для маршрута конвейера.
/// </summary>
public struct SetRouteItemRequest : IRequestCleanup { public Entity RouteEntity; public int NewItemID; }

/// <summary>
/// Запрос на переключение активности маршрута конвейера.
/// </summary>
public struct ToggleRouteRequest : IRequestCleanup { public Entity RouteEntity; }

/// <summary>
/// Запрос на открытие UI управления маршрутами конвейеров.
/// </summary>
public struct OpenConveyorRoutesUIRequest : IRequestCleanup { }

/// <summary>
/// Запрос на подтверждение размещения конвейера.
/// </summary>
public struct ConfirmConveyorPlacementRequest : IRequestCleanup { public int ItemID; public Entity PreviewHolder; public Entity StartConnector; public Entity EndConnector; }

/// <summary>
/// Запрос на удаление конвейера под курсором.
/// </summary>
public struct RemoveConveyorUnderCursorRequest : IRequestCleanup { }

/// <summary>
/// Запрос на открытие или закрытие исследования.
/// </summary>
public struct ToggleResearchUIRequest : IRequestCleanup { }

/// <summary>
/// Запрос на мгновенное изучение технологии.
/// </summary>
public struct StartResearchRequest : IRequestCleanup { public int TechnologyId; }