using Unity.Entities;

/// <summary>
/// Тег-компонент, указывающий, что сущность игрока "мертва"
/// и не должна обрабатываться системами движения, ввода и т.д.
/// </summary>
public struct DeadTag : IComponentData { }