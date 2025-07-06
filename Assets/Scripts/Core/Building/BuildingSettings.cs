using Unity.Entities;
using UnityEngine;

/// <summary>
/// Синглтон-компонент, хранящий глобальные настройки для системы строительства.
/// Позволяет конфигурировать слои и углы наклона через инспектор.
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
    /// ID валидного материала для превью (для рендера).
    /// </summary>
    public UnityEngine.Rendering.BatchMaterialID ValidPlacementMaterialID;

    /// <summary>
    /// ID невалидного материала для превью (для рендера).
    /// </summary>
    public UnityEngine.Rendering.BatchMaterialID InvalidPlacementMaterialID;
}