using Unity.Entities;

// ТЕГИ, ОПРЕДЕЛЯЮЩИЕ ТЕКУЩИЙ РЕЖИМ ИГРЫ 

/// <summary>
/// Тег-компонент, указывающий, что игра находится в стандартном режиме (перемещение, взаимодействие).
/// </summary>
public struct InDefaultMode : IComponentData { }

/// <summary>
/// Тег-компонент, указывающий, что игра находится в режиме строительства.
/// </summary>
public struct InBuildingMode : IComponentData { }

/// <summary>
/// Тег-компонент, указывающий, что на экране открыт какой-либо UI, блокирующий геймплей.
/// </summary>
public struct InUIMode : IComponentData { }


// КОМПОНЕНТЫ С ДАННЫМИ, СПЕЦИФИЧНЫМИ ДЛЯ РЕЖИМА 

/// <summary>
/// Компонент, хранящий данные, необходимые только для режима строительства.
/// Добавляется и удаляется вместе с тегом InBuildingMode.
/// </summary>
public struct BuildingState : IComponentData
{
    /// <summary>
    /// Префаб сущности здания, который в данный момент выбран для размещения.
    /// </summary>
    public Entity BuildingPrefabToPlace;

    /// <summary>
    /// ID предмета, соответствующий BuildingPrefabToPlace.
    /// </summary>
    public int BuildingItemID;
}

/// <summary>
/// Компонент, хранящий данные, необходимые только для режима UI.
/// Добавляется и удаляется вместе с тегом InUIMode.
/// </summary>
public struct UIState : IComponentData
{
    /// <summary>
    /// Хранит информацию о том, какой UI сейчас должен быть активен.
    /// </summary>
    public UIType ActiveUIType;

    /// <summary>
    /// Хранит сущность, для которой открыт UI (например, конкретный NPC или поселение).
    /// </summary>
    public Entity ActiveUITarget;
}