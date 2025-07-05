using Unity.Entities;

// Перечисление UIType остается, так как оно полезно для компонента UIState
public enum UIType
{
    None,
    Inventory,
    NPC,
    Settlement
}

/// <summary>
/// Компонент-маркер для создания глобальной сущности-синглтона,
/// которая будет носителем тегов состояния (InDefaultMode, InUIMode и т.д.).
/// Все данные о состоянии вынесены в отдельные, специфичные для режима компоненты.
/// </summary>
public struct GameState : IComponentData { }