using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Интерфейс-маркер для всех компонентов-запросов,
/// которые должны быть удалены в конце кадра системой RequestCleanupSystem.
/// </summary>
public interface IRequestCleanup : IComponentData { }

/// <summary>
/// Запрос на вход в режим строительства.
/// </summary>
public struct EnterBuildingModeRequest : IRequestCleanup
{
    /// <summary>
    /// ID предмета, который нужно построить.
    /// </summary>
    public int ItemID;
}

/// <summary>
/// Запрос на выход из режима строительства.
/// </summary>
public struct ExitBuildingModeRequest : IRequestCleanup { }

/// <summary>
/// Запрос на размещение здания в конкретной точке мира.
/// Теперь содержит позицию и вращение для точного размещения.
/// </summary>
public struct PlaceBuildingRequest : IRequestCleanup
{
    public float3 Position;
    public quaternion Rotation;
    public Entity BuildingPrefabToPlace; // <-- ДОБАВЛЕНО
    public int ItemIDToConsume;          // <-- ДОБАВЛЕНО
}


/// <summary>
/// Запрос на переключение состояния инвентаря (открыть/закрыть).
/// </summary>
public struct ToggleInventoryRequest : IRequestCleanup { }

/// <summary>
/// Запрос на открытие пользовательского интерфейса NPC для указанной сущности.
/// </summary>
public struct OpenNPCUIRequest : IRequestCleanup
{
    /// <summary>
    /// Целевая сущность NPC.
    /// </summary>
    public Entity Target;
}

/// <summary>
/// Запрос на открытие пользовательского интерфейса поселения для указанной сущности.
/// </summary>
public struct OpenSettlementUIRequest : IRequestCleanup
{
    /// <summary>
    /// Целевая сущность поселения.
    /// </summary>
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

/// <summary>
/// Запрос на найм NPC.
/// </summary>
public struct HireNPCRequest : IRequestCleanup
{
    /// <summary>
    /// Сущность NPC, которую нужно нанять.
    /// </summary>
    public Entity NPCToHire;
}

/// <summary>
/// Запрос на назначение NPC на выполнение задачи.
/// </summary>
public struct AssignNPCToTaskRequest : IRequestCleanup
{
    /// <summary>
    /// Сущность NPC, которую нужно назначить.
    /// </summary>
    public Entity NPC;

    /// <summary>
    /// Сущность целевого ресурсного узла для задачи.
    /// </summary>
    public Entity TargetResourceNode;
}