using Unity.Entities;

/// <summary>
/// Тег, сигнализирующий, что превью объекта "приклеено" к endpoint-у и не должно позиционироваться по мышке.
/// </summary>
public struct SnapToEndpointTag : IComponentData { }