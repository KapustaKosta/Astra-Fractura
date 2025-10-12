using Unity.Entities;
using UnityEngine;
using Unity.Rendering;

/// <summary>
/// Глобальные настройки строительства. Материалы задаются в инспекторе:
/// - validPlacementMaterial / invalidPlacementMaterial — материал превью (зелёный/красный)
/// - resourceHighlightMaterial — материал подсветки РЕСУРСНОГО УЗЛА в момент установки карьера
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

    [Range(0f, 90f)] public float maxPlacementSlopeAngle = 25f;
    public float maxPlacementDistance = 100f;

    [Header("Preview Materials (Preview entity)")]
    [Tooltip("Материал превью, когда размещение валидно.")]
    public Material validPlacementMaterial;
    [Tooltip("Материал превью, когда размещение невалидно.")]
    public Material invalidPlacementMaterial;

    [Header("Quarry Resource Highlight (Resource Node entity)")]
    [Tooltip("Материал подсветки ресурсного узла во время установки карьера.")]
    public Material resourceHighlightMaterial;

    private class Baker : Baker<BuildingSettingsAuthoring>
    {
        public override void Bake(BuildingSettingsAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new BuildingSettings
            {
                BuildableSurfaceLayerMask = authoring.buildableSurfaceLayer.value,
                ObstacleLayerMask         = authoring.obstacleLayer.value,
                MaxPlacementSlopeAngle    = authoring.maxPlacementSlopeAngle,
                MaxPlacementDistance      = authoring.maxPlacementDistance,
                PreviewLayer              = GetFirstLayer(authoring.previewLayer),

                // ID материалов регистрируются рантайм-системой,
                // здесь просто заполняем default.
                ValidPlacementMaterialID     = default,
                InvalidPlacementMaterialID   = default,
                ResourceHighlightMaterialID  = default
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
                if ((value & (1 << i)) != 0) return i;
            return -1;
        }
    }
}

