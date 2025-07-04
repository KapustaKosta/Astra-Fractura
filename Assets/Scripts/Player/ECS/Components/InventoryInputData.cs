using Unity.Entities;

/// <summary>
/// ECS-компонент-флаг, указывающий на однократное нажатие кнопки инвентаря в текущем кадре.
/// Используется для обработки событий, которые должны происходить только один раз при нажатии кнопки.
/// </summary>
public struct InventoryInputData : IComponentData
{
    /// <summary>
    /// Флаг, который устанавливается в true, если кнопка инвентаря была нажата в текущем кадре.
    /// </summary>
    public bool InventoryPressed;
}