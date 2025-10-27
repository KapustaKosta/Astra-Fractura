using Unity.Entities;
using UnityEngine;
using Unity.Rendering;
using Unity.Mathematics;

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

    [Header("Quarry Resource Highlight (Overlay)")]
    [Tooltip("URP Unlit, Surface=Transparent — будет отрисован поверх ресурса.")]
    public Material resourceHighlightOverlayMaterial;
    [Tooltip("RGB подсветки ресурса.")]
    public Color resourceHighlightColor = new Color(0.20f, 0.50f, 1.00f, 1.0f);
    [Range(0f, 1f)]
    [Tooltip("Прозрачность вуали.")]
    public float resourceHighlightAlpha = 0.5f;

    [Header("Quarry Range Visualization (fallback/aux)")]
    public Mesh     quarryRangeMesh;
    public Material quarryRangeMaterial;

    private class Baker : Baker<BuildingSettingsAuthoring>
    {
        public override void Bake(BuildingSettingsAuthoring a)
        {
            var e = GetEntity(TransformUsageFlags.None);

            float4 hlColor = new float4(a.resourceHighlightColor.r,
                                        a.resourceHighlightColor.g,
                                        a.resourceHighlightColor.b,
                                        a.resourceHighlightColor.a);

            AddComponent(e, new BuildingSettings
            {
                BuildableSurfaceLayerMask = a.buildableSurfaceLayer.value,
                ObstacleLayerMask         = a.obstacleLayer.value,
                MaxPlacementSlopeAngle    = a.maxPlacementSlopeAngle,
                MaxPlacementDistance      = a.maxPlacementDistance,
                PreviewLayer              = GetFirstLayer(a.previewLayer),

                ValidPlacementMaterialID           = default,
                InvalidPlacementMaterialID         = default,

                ResourceHighlightOverlayMaterialID = default,
                ResourceHighlightColor             = hlColor,
                ResourceHighlightAlpha             = Mathf.Clamp01(a.resourceHighlightAlpha),

                QuarryRangeMaterialID              = default,
                QuarryRangeMeshID                  = default
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
