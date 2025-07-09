using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Интерфейс-маркер для всех компонентов-запросов,
/// которые должны быть удалены в конце кадра системой RequestCleanupSystem.
/// </summary>
public interface IRequestCleanup : IComponentData { }

// Запросы строительства

/// <summary>
/// Запрос на вход в режим строительства.
/// </summary>
public struct EnterBuildingModeRequest : IRequestCleanup
{
    public int ItemID;
}

/// <summary>
/// Запрос на выход из режима строительства.
/// </summary>
public struct ExitBuildingModeRequest : IRequestCleanup { }

/// <summary>
/// Запрос на размещение здания в конкретной точке мира.
/// </summary>
public struct PlaceBuildingRequest : IRequestCleanup
{
    public float3 Position;
    public quaternion Rotation;
    public Entity BuildingPrefabToPlace; 
    public int ItemIDToConsume;          
}

// Запросы UI и взаимодействий 

/// <summary>
/// Запрос на переключение состояния инвентаря (открыть/закрыть).
/// </summary>
public struct ToggleInventoryRequest : IRequestCleanup { }

/// <summary>
/// Запрос на открытие пользовательского интерфейса NPC.
/// </summary>
public struct OpenNPCUIRequest : IRequestCleanup
{
    public Entity Target;
}

/// <summary>
/// Запрос на открытие пользовательского интерфейса поселения.
/// </summary>
public struct OpenSettlementUIRequest : IRequestCleanup
{
    public Entity Target;
}

/// <summary>
/// Запрос на закрытие всех активных пользовательских интерфейсов.
/// </summary>
public struct CloseAllUIRequest : IRequestCleanup { }

/// <summary>
/// Запрос на выполнение взаимодействия с чем-либо в мире.
/// </summary>
public struct InteractionRequest : IRequestCleanup { }

// запросы NPC 

/// <summary>
/// Запрос на найм NPC.
/// </summary>
public struct HireNPCRequest : IRequestCleanup
{
    public Entity NPCToHire;
}

/// <summary>
/// Запрос на назначение NPC на выполнение задачи.
/// </summary>
public struct AssignNPCToTaskRequest : IRequestCleanup
{
    public Entity NPC;
    public Entity TargetResourceNode;
}

// Запросы инвентаря

/// <summary>
/// Запрос на добавление предмета в инвентарь конкретной сущности.
/// </summary>
public struct AddItemRequest : IRequestCleanup
{
    public Entity TargetInventoryOwner; // Сущность-владелец инвентаря
    public int ItemID;
    public int Amount;
}

/// <summary>
/// Запрос на удаление предмета из инвентаря конкретной сущности.
/// </summary>
public struct RemoveItemRequest : IRequestCleanup
{
    public Entity TargetInventoryOwner; 
    public int ItemID;
    public int Amount;
}