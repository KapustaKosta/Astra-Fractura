using Unity.Entities;
using UnityEngine;
using Conveyor;

namespace Conveyor.Authoring
{
    public class ConveyorBeltAuthoring : MonoBehaviour
    {
        [Header("Segment length limits (meters)")]
        public float BaseLength = 7f;
        public float MinLength = 1f;
        public float MaxLength = 7f;

        [Header("Logistics")]
        [Tooltip("Сколько предметов может пройти через один сегмент в минуту.")]
        public float ItemsPerMinute = 120f;
        [Tooltip("Скорость перемещения предметов по ленте в метрах/секунду.")]
        public float SpeedMetersPerSecond = 2.0f;

        [Header("Auto-measure (bounds)")]
        public bool AutoMeasureBounds = true;
        public Vector3 LengthAxis = Vector3.forward;

        class Baker : Baker<ConveyorBeltAuthoring>
        {
            public override void Bake(ConveyorBeltAuthoring a)
            {
                var e = GetEntity(TransformUsageFlags.Renderable);

                float baseLen = Mathf.Max(0.001f, a.BaseLength);

                if (a.AutoMeasureBounds)
                {
                    var renderers = a.GetComponentsInChildren<Renderer>();
                    if (renderers != null && renderers.Length > 0)
                    {
                        var b = renderers[0].bounds;
                        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

                        var axis = a.LengthAxis.normalized;
                        var size = b.size;
                        float projectX = Mathf.Abs(Vector3.Dot(axis, Vector3.right));
                        float projectY = Mathf.Abs(Vector3.Dot(axis, Vector3.up));
                        float projectZ = Mathf.Abs(Vector3.Dot(axis, Vector3.forward));
                        baseLen = projectX > projectY && projectX > projectZ ? size.x :
                                  projectY > projectZ ? size.y : size.z;

                        baseLen = Mathf.Max(0.001f, baseLen);
                    }
                }

                AddComponent(e, new ConveyorSegmentSettings
                {
                    Length = baseLen,
                    MinLength = Mathf.Max(0.001f, a.MinLength),
                    MaxLength = Mathf.Max(a.MinLength, a.MaxLength),
                    ItemsPerMinute = Mathf.Max(1f, a.ItemsPerMinute),
                    Speed = Mathf.Max(0.1f, a.SpeedMetersPerSecond)
                });
            }
        }
    }
}