using Unity.Entities;

/// <summary>
/// Компонент-тег для идентификации сущности игрока.
/// Позволяет легко находить игрока в ECS-запросах.
/// </summary>
public struct PlayerTag : IComponentData { }