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

    [Tooltip("Слой, на который будет временно помещено превью здания.")]
    public LayerMask previewLayer;
    
    [Range(0f, 90f)]
    [Tooltip("Максимальный угол наклона поверхности, на которой можно строить.")]
    public float maxPlacementSlopeAngle = 25f;

    [Tooltip("Максимальная дистанция от камеры для размещения здания.")]
    public float maxPlacementDistance = 100f;

    [Header("Preview Materials")]
    [Tooltip("Материал для превью, когда размещение валидно.")]
    public Material validPlacementMaterial;
    [Tooltip("Материал для превью, когда размещение невалидно.")]
    public Material invalidPlacementMaterial;

    private class Baker : Baker<BuildingSettingsAuthoring>
    {
        public override void Bake(BuildingSettingsAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None); 

            AddComponent(entity, new BuildingSettings
            {
                BuildableSurfaceLayerMask = authoring.buildableSurfaceLayer.value,
                ObstacleLayerMask = authoring.obstacleLayer.value,
                MaxPlacementSlopeAngle = authoring.maxPlacementSlopeAngle,
                MaxPlacementDistance = authoring.maxPlacementDistance,
                PreviewLayer = GetFirstLayer(authoring.previewLayer),
                ValidPlacementMaterialID = default, 
                InvalidPlacementMaterialID = default
            });
        }

        /// <summary>
        /// Получает индекс первого включенного слоя из LayerMask.
        /// </summary>
        private static int GetFirstLayer(LayerMask mask)
        {
            int value = mask.value;
            if (value == 0) return -1;
            for (int i = 0; i < 32; i++)
            {
                if ((value & (1 << i)) != 0) return i;
            }
            return -1;
        }
    }
}