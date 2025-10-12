using Unity.Entities;
using UnityEngine.Rendering;

/// <summary>
/// Тег для идентификации всех сущностей-карьеров.
/// </summary>
public struct QuarryTag : IComponentData { }

/// <summary>
/// Компонент, хранящий неизменяемые настройки карьера.
/// </summary>
public struct QuarrySettings : IComponentData
{
    public float HarvestInterval;
    public float InteractionRange;
    public float EnergyConsumptionKW;
}

/// <summary>
/// Компонент для хранения динамического состояния карьера.
/// </summary>
public struct QuarryState : IComponentData
{
    public float LastHarvestTime;
    public Entity TargetResourceNode;
}

/// <summary>
/// Компонент-тег, указывающий, что внутренний инвентарь карьера
/// полностью заполнен. Управляется системой QuarryInventoryStateSystem.
/// </summary>
public struct QuarryInventoryFullTag : IComponentData { }

/// <summary>
/// Тег, который предполагается существующим в вашей энергосистеме.
/// Он должен добавляться к потребителю, когда сеть обеспечивает его энергией.
/// </summary>
public struct HasPowerTag : IComponentData { }

/// <summary>
/// Тег, который добавляется к префабу карьера и его превью,
/// чтобы системы строительства могли применять к нему особую логику.
/// </summary>
public struct QuarryPlacementTag : IComponentData { }

/// <summary>
/// Компонент, добавляемый к превью карьера. Хранит ссылку
/// на ресурсный узел, который будет целью для добычи.
/// </summary>
public struct QuarryPreviewTarget : IComponentData, IEnableableComponent
{
    public Entity TargetNode;
}

/// <summary>
/// Тег, добавляемый к ресурсному узлу, чтобы система рендеринга
/// применила к нему материал подсветки.
/// </summary>
public struct HighlightedResourceNodeTag : IComponentData { }

/// <summary>
/// Компонент, добавляемый к превью карьера.
/// Хранит оригинальный материал
/// </summary>
public struct ResourceOriginalMaterial : IComponentData { public BatchMaterialID Value; }