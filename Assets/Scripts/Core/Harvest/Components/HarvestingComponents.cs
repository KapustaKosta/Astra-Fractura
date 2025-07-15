using Unity.Entities;

/// <summary>
/// Тег-намерение. Добавляется к сущности, когда она хочет начать добычу.
/// Управляется системой ввода игрока или AI-планировщиком.
/// </summary>
public struct WantsToHarvestTag : IComponentData { }

/// <summary>
/// Тег, добавляемый к сущности "добытчика" в момент, когда он успешно добывает ресурс.
/// Используется UI для отображения информации о добыче.
/// </summary>
public struct IsHarvestingTag : IComponentData
{
    public ResourceCollectionType ResourceType;
}

/// <summary>
/// Компонент, хранящий динамическое состояние процесса добычи для сущности.
/// </summary>
public struct HarvesterState : IComponentData
{
    public float LastHarvestTime;
}

/// <summary>
/// Компонент, хранящий индивидуальные настройки добычи для сущности.
/// </summary>
public struct HarvesterSettings : IComponentData
{
    public float HarvestInterval;
    public float InteractionRange;
}