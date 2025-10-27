using Unity.Entities;
using Unity.Rendering;            
using Unity.Mathematics;
using UnityEngine.Rendering;

/// <summary>
/// Глобальные настройки строительства + зарегистрированные графические ресурсы.
/// Заполняется из BuildingSettingsAuthoring.Baker и в рантайме (MaterialRegistrationSystem).
/// </summary>
public struct BuildingSettings : IComponentData
{
    // Правила размещения/проверок
    public int   BuildableSurfaceLayerMask;
    public int   ObstacleLayerMask;
    public float MaxPlacementSlopeAngle;

    /// <summary>
    /// Максимальная дистанция от камеры для размещения здания.
    /// </summary>
    public float MaxPlacementDistance;

    /// <summary>
    /// Слой, на который помещается превью здания.
    /// </summary>
    public int PreviewLayer;

    // Материалы превью
    public BatchMaterialID ValidPlacementMaterialID;
    public BatchMaterialID InvalidPlacementMaterialID;

    // Подсветка ресурса (overlay поверх исходного материала)
    public BatchMaterialID ResourceHighlightOverlayMaterialID; // URP Unlit Transparent
    public float4          ResourceHighlightColor;             // RGB берём отсюда
    public float           ResourceHighlightAlpha;             // 0..1 — прозрачность вуали

    // Визуализация радиуса (и fallback)
    public BatchMeshID     QuarryRangeMeshID;
    public BatchMaterialID QuarryRangeMaterialID;
}