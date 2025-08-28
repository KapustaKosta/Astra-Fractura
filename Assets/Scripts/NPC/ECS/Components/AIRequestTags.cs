using Unity.Entities;

/// <summary>
/// Тег-запрос. Указывает на намерение сущности выполнить действие по добыче ресурсов.
/// </summary>
public struct HarvestRequestTag : IComponentData { }

/// <summary>
/// Тег-запрос. Указывает, что NPC достиг базы и готов начать процесс разгрузки инвентаря.
/// </summary>
public struct UnloadRequestTag : IComponentData { }

/// <summary>
/// Одноразовый запрос от игрока на назначение NPC задачи по добыче ресурса.
/// </summary>
public struct PlayerAssignHarvestRequest : IComponentData
{
    public Entity TargetNPC;
    public Entity TargetResourceNode;
}

/// <summary>
/// Одноразовый запрос от UI на назначение NPC для обслуживания цеха.
/// </summary>
public struct AssignWorkerToWorkshopRequest : IComponentData
{
    public Entity NpcEntity;
    public Entity WorkshopEntity;
}