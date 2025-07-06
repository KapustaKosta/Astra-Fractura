using Unity.Entities;
using UnityEngine;
using Unity.Rendering;

/// <summary>
/// Authoring-компонент для глобальных настроек системы строительства.
/// Позволяет задать слои и углы наклона через инспектор и преобразовать их в ECS-синглтон.
/// </summary>
public class BuildingSettingsAuthoring : MonoBehaviour
{
    [Header("Placement Rules")]
    [Tooltip("Слои, на которых можно размещать здания.")]
    public LayerMask buildableSurfaceLayer;
    
    [Tooltip("Слои, объекты на которых считаются препятствиями для строительства.")]
    public LayerMask obstacleLayer;
    
    [Range(0f, 90f)]
    [Tooltip("Максимальный угол наклона поверхности, на которой можно строить.")]
    public float maxPlacementSlopeAngle = 25f;

    [Header("Preview Materials")]
    [Tooltip("Материал для превью, когда размещение валидно.")]
    public Material validPlacementMaterial;
    [Tooltip("Материал для превью, когда размещение невалидно.")]
    public Material invalidPlacementMaterial;

    /// <summary>
    /// Вложенный класс Baker, который преобразует данные из Authoring-компонента
    /// в ECS-компоненты во время запекания.
    /// </summary>
    private class Baker : Baker<BuildingSettingsAuthoring>
    {
        public override void Bake(BuildingSettingsAuthoring authoring)
        {
            // Создаем синглтон-сущность. Использование TransformUsageFlags.None указывает,
            // что эта сущность не привязана к Transform в сцене.
            var entity = GetEntity(TransformUsageFlags.None); 
            
            // Добавляем компонент BuildingSettings к созданной сущности.
            // Этот компонент будет синглтоном в ECS.
            AddComponent(entity, new BuildingSettings
            {
                // Передаем значения LayerMask из Authoring-компонента.
                BuildableSurfaceLayerMask = authoring.buildableSurfaceLayer.value,
                ObstacleLayerMask = authoring.obstacleLayer.value,
                MaxPlacementSlopeAngle = authoring.maxPlacementSlopeAngle,
                // MaterialID для превью будут инициализированы в рантайме в PreviewMaterialSystem.
                ValidPlacementMaterialID = default, 
                InvalidPlacementMaterialID = default
            });
        }
    }
}