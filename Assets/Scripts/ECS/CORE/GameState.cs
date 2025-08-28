using Unity.Entities;

// Перечисление UIType остается, так как оно полезно для компонента UIState
public enum UIType
{
    None,
    Inventory,
    NPC,
    Settlement,
    Trade,
    Generator,
    Battery,
    Production,
    Workshop,
    ConveyorRoutes
}