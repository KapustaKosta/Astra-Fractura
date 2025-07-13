using Unity.Entities;

/// <summary>
/// Тег-компонент, указывающий, что сущность NPC была нанята.
/// Используется для фильтрации и логики, применимой только к нанятым NPC.
/// </summary>
public struct NPCHiredTag : IComponentData {}