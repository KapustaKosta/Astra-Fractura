using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine.Rendering;

/// <summary>
/// Компонент-тег для идентификации всех сущностей, являющихся Карьерами.
/// Используется для запросов и общей идентификации.
/// </summary>
public struct QuarryTag : IComponentData { }

/// <summary>
/// Содержит неизменяемые настройки для Карьера, задаваемые в префабе.
/// </summary>
public struct QuarrySettings : IComponentData
{
    /// <summary>
    /// Максимальное расстояние, на котором карьер может взаимодействовать с узлом ресурсов.
    /// </summary>
    public float InteractionRange;
    /// <summary>
    /// Базовое время в секундах между циклами добычи ресурсов.
    /// </summary>
    public float HarvestInterval;
    /// <summary>
    /// Энергопотребление в киловаттах (кВт) при активной работе карьера.
    /// </summary>
    public float EnergyConsumptionKW;
}

/// <summary>
/// Содержит изменяемое состояние Карьера в процессе игры.
/// </summary>
public struct QuarryState : IComponentData
{
    /// <summary>
    /// Флаг, указывающий, активен ли карьер в данный момент (работает ли).
    /// </summary>
    public bool  IsOnline;
    /// <summary>
    /// Время (ElapsedTime) последней успешной добычи ресурсов.
    /// </summary>
    public float LastHarvestTime;
    /// <summary>
    /// Сущность узла ресурсов, который в данный момент назначен этому карьеру.
    /// </summary>
    public Entity TargetResourceNode;
}

/// <summary>
/// Компонент-тег, который добавляется сущности-превью карьера на время его размещения игроком.
/// </summary>
public struct QuarryPlacementTag : IComponentData { }

/// <summary>
/// Компонент, содержащий ссылку на узел ресурсов, на который нацелен превью карьера.
/// Является `IEnableableComponent`, что позволяет включать и выключать его,
/// эффективно управляя состоянием "нацеливания" без удаления компонента.
/// </summary>
public struct QuarryPreviewTarget : IComponentData, IEnableableComponent
{
    /// <summary>
    /// Сущность узла ресурсов, на который указывает превью.
    /// </summary>
    public Entity TargetNode;
}

/// <summary>
/// Хранит оригинальный ID материала узла ресурсов до того, как он был изменен для подсветки.
/// Используется для восстановления исходного вида после снятия подсветки.
/// </summary>
public struct ResourceOriginalMaterial : IComponentData { public BatchMaterialID Value; }

/// <summary>
/// Компонент-тег, указывающий, что инвентарь карьера заполнен, и он не может больше добывать ресурсы.
/// </summary>
public struct QuarryInventoryFullTag : IComponentData { }

/// <summary>
/// Компонент-тег, указывающий, что карьер в данный момент получает достаточно энергии для работы.
/// </summary>
public struct HasPowerTag : IComponentData { }

/// <summary>
/// Компонент-тег, который добавляется на корневую сущность узла ресурсов,
/// чтобы пометить его как подсвеченный (например, при наведении курсора в режиме строительства карьера).
/// </summary>
public struct HighlightedResourceNodeTag : IComponentData { }

/// <summary>
/// Хранит оригинальный базовый цвет (BaseColor) материала узла ресурсов.
/// Используется для восстановления цвета после того, как он был временно изменен для подсветки.
/// </summary>
public struct ResourceOriginalBaseColor : IComponentData
{
    public float4 Value;
}

/// <summary>
/// Компонент-синглтон, который отслеживает глобальное состояние подсветки узлов ресурсов
/// во время размещения карьера.
/// </summary>
public struct QuarryPreviewHighlightState : IComponentData
{
    /// <summary>
    /// Сущность узла ресурсов, который был подсвечен в предыдущем кадре.
    /// Необходимо для того, чтобы знать, с какого узла нужно снять подсветку.
    /// </summary>
    public Entity LastHighlightedNode;
}

/// <summary>
/// Элемент динамического буфера, который хранит ссылки на сущности-"призраки" (прокси),
/// созданные для визуального эффекта подсветки поверх оригинальных моделей узла ресурсов.
/// </summary>
public struct ResourceHighlightProxyBuffer : IBufferElementData
{
    public Entity Proxy;
}