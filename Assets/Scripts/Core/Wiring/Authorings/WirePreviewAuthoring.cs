using Unity.Entities;
using UnityEngine;

namespace Wiring
{
    /// <summary> Authoring: накатить настройки превью в WirePreviewConfig (синглтон). </summary>
    [DisallowMultipleComponent]
    public sealed class WirePreviewAuthoring : MonoBehaviour
    {
        [Header("Префаб превью (Entities Graphics) — длина вдоль +Z, scale=1")]
        public GameObject PreviewPrefab;

        [Min(0.001f)] public float Thickness = 0.03f;

        [Header("Магнит и фильтры физики")]
        [Min(0f)] public float MagnetRadius = 0.6f;
        public uint GroundCategory = ~0u;
        public uint ConnectorCategory = ~0u;

        [Header("Сглаживание превью (s^-1)")]
        [Min(0f)] public float SmoothRotationSpeed = 20f;
        [Min(0f)] public float SmoothLengthSpeed = 20f;

        [Header("HUD подсказки")]
        [Min(0f)] public float CostLabelHeight = 0.25f;

        private sealed class Baker : Baker<WirePreviewAuthoring>
        {
            public override void Bake(WirePreviewAuthoring a)
            {
                var e = GetEntity(TransformUsageFlags.None);

                AddComponent(e, new WirePreviewConfig
                {
                    Prefab = a.PreviewPrefab ? GetEntity(a.PreviewPrefab, TransformUsageFlags.Dynamic) : Entity.Null,
                    Thickness = a.Thickness,
                    MagnetRadius = a.MagnetRadius,
                    GroundCategory = a.GroundCategory,
                    ConnectorCategory = a.ConnectorCategory,
                    SmoothRotationSpeed = a.SmoothRotationSpeed,
                    SmoothLengthSpeed = a.SmoothLengthSpeed,
                    CostLabelHeight = a.CostLabelHeight
                });
            }
        }
    }
}
