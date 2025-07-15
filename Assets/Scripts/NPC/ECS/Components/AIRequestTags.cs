using Unity.Entities;

/// <summary>
/// Тег-запрос. Указывает на намерение сущности выполнить действие по добыче ресурсов.
/// Добавляется системой выполнения цели (GoalExecutionSystem) и считывается системой добычи (HarvestingSystem).
/// </summary>
public struct HarvestRequestTag : IComponentData {}

/// <summary>
/// Тег-запрос. Указывает, что NPC достиг базы и готов начать процесс разгрузки инвентаря.
/// Добавляется системой ReturnToBaseGoalExecutionSystem и считывается NPCUnloadSystem.
/// </summary>
public struct UnloadRequestTag : IComponentData {}

/// <summary>
/// Одноразовый запрос от игрока на назначение NPC задачи по добыче ресурса.
/// </summary>
public struct PlayerAssignHarvestRequest : IComponentData
{
    public Entity TargetNPC;
    public Entity TargetResourceNode;
}