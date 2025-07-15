using Unity.Entities;

/// <summary>
/// Универсальный компонент, хранящий сущность, которая является
/// текущей активной целью для другой сущности (для добычи, атаки, взаимодействия и т.д.).
/// </summary>
public struct ActiveTarget : IComponentData
{
    /// <summary>
    /// Сущность, являющаяся целью.
    /// </summary>
    public Entity Value;
}