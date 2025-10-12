using Unity.Entities;
using UnityEngine;

/// <summary>
/// Синглтон-компонент, хранящий глобальные настройки для системы строительства.
/// </summary>
public struct BuildingSettings : IComponentData
{
    /// <summary>
    /// Маска слоев, на которых можно размещать здания.
    /// </summary>
    public int BuildableSurfaceLayerMask;

    /// <summary>
    /// Маска слоев, объекты на которых считаются препятствиями для строительства.
    /// </summary>
    public int ObstacleLayerMask;

    /// <summary>
    /// Максимальный угол наклона поверхности, на которой можно строить (в градусах).
    /// </summary>
    public float MaxPlacementSlopeAngle;

    /// <summary>
    /// Максимальная дистанция от камеры для размещения здания.
    /// </summary>
    public float MaxPlacementDistance;

    /// <summary>
    /// Слой, на который помещается превью здания.
    /// </summary>
    public int PreviewLayer;

    // Материалы превью (валид/невалид)
    public UnityEngine.Rendering.BatchMaterialID ValidPlacementMaterialID;

    /// <summary>
    /// ID невалидного материала для превью (для рендера).
    /// </summary>
    public UnityEngine.Rendering.BatchMaterialID InvalidPlacementMaterialID;

    // материал подсветки ресурсного узла во время установки карьера
    public UnityEngine.Rendering.BatchMaterialID ResourceHighlightMaterialID;
}