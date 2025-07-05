using Unity.Entities;

/// <summary>
/// Компонент, динамически добавляемый к игроку. Хранит сущность,
/// на которую игрок смотрит в данный момент.
/// </summary>
public struct InteractionTarget : IComponentData
{
    public Entity Value;
}

/// <summary>
/// Тег-намерение. Добавляется к игроку, когда он зажал ЛКМ,
/// смотрит на ресурс и готов начать добычу.
/// </summary>
public struct WantsToHarvestTag : IComponentData { }

/// <summary>
/// Тег, добавляемый к сущности игрока в момент, когда он успешно добывает ресурс.
/// Используется UI для отображения информации о добыче.
/// </summary>
public struct IsHarvestingTag : IComponentData
{
    public ResourceCollectionType ResourceType;
}

/// <summary>
/// Компонент-запрос на выполнение одного акта добычи ресурса.
/// Создается HarvestingSystem и обрабатывается ProcessHarvestRequestSystem.
/// </summary>
public struct HarvestRequest : IComponentData
{
    public Entity Player;
    public Entity TargetResourceNode;
}