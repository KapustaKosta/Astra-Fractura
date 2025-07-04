using Unity.Entities;

/// <summary>
/// Перечисление всех возможных глобальных состояний игры.
/// </summary>
public enum GameMode
{
    /// <summary>
    /// Обычный режим игры от первого лица.
    /// </summary>
    Default,

    /// <summary>
    /// Режим строительства.
    /// </summary>
    Building,

    /// <summary>
    /// Любой полноэкранный UI (инвентарь, меню NPC и т.д.).
    /// </summary>
    UI
}

/// <summary>
/// Перечисление типов UI, чтобы знать, какой именно UI активен.
/// </summary>
public enum UIType
{
    /// <summary>
    /// UI не активен.
    /// </summary>
    None,

    /// <summary>
    /// Активен UI инвентаря.
    /// </summary>
    Inventory,

    /// <summary>
    /// Активен UI NPC.
    /// </summary>
    NPC,

    /// <summary>
    /// Активен UI поселения.
    /// </summary>
    Settlement
}

/// <summary>
/// ECS синглтон-компонент, который является ЕДИНСТВЕННЫМ источником правды
/// о текущем глобальном состоянии игры.
/// </summary>
public struct GameState : IComponentData
{
    /// <summary>
    /// Текущий глобальный режим игры.
    /// </summary>
    public GameMode CurrentMode;

    /// <summary>
    /// Хранит информацию о том, какой UI сейчас должен быть активен, если CurrentMode = UI.
    /// </summary>
    public UIType ActiveUIType;

    /// <summary>
    /// Хранит сущность, для которой открыт UI (например, конкретный NPC или поселение).
    /// </summary>
    public Entity ActiveUITarget;

    /// <summary>
    /// Префаб сущности здания, который в данный момент выбран для размещения в режиме строительства.
    /// </summary>
    public Entity BuildingPrefabToPlace;

    /// <summary>
    /// ID предмета, соответствующий BuildingPrefabToPlace.
    /// </summary>
    public int BuildingItemID;
}