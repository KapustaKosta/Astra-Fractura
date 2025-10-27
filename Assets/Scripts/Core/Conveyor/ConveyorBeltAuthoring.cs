using Unity.Entities;
using UnityEngine;
using Conveyor;

namespace Conveyor.Authoring
{
    /// <summary>
    /// Компонент "авторинга" для префабов сегментов конвейерной ленты.
    /// Позволяет настроить параметры сегмента в инспекторе Unity,
    /// которые затем будут "запечены" в ECS-компоненты.
    /// </summary>
    public class ConveyorBeltAuthoring : MonoBehaviour
    {
        [Header("Segment length limits (meters)")]
        [Tooltip("Базовая длина модели сегмента. Может быть вычислена автоматически.")]
        public float BaseLength = 7f;
        [Tooltip("Минимальная длина, до которой можно сжать сегмент.")]
        public float MinLength = 1f;
        [Tooltip("Максимальная длина, до которой можно растянуть сегмент.")]
        public float MaxLength = 7f;

        [Header("Logistics")]
        [Tooltip("Сколько предметов может пройти через один сегмент в минуту.")]
        public float ItemsPerMinute = 120f;
        [Tooltip("Скорость перемещения предметов по ленте в метрах/секунду.")]
        public float SpeedMetersPerSecond = 2.0f;

        [Header("Energy")]
        [Tooltip("Энергопотребление одного сегмента в кВт.")]
        public float EnergyConsumptionPerSegmentKW = 0.1f;

        [Header("Auto-measure (bounds)")]
        [Tooltip("Включить автоматическое измерение базовой длины по габаритам модели.")]
        public bool AutoMeasureBounds = true;
        [Tooltip("Ось, вдоль которой измеряется длина модели.")]
        public Vector3 LengthAxis = Vector3.forward;

        /// <summary>
        /// Класс-бейкер, который преобразует данные из ConveyorBeltAuthoring
        /// в ECS-компоненты во время процесса "запекания" (baking).
        /// </summary>
        class Baker : Baker<ConveyorBeltAuthoring>
        {
            public override void Bake(ConveyorBeltAuthoring a)
            {
                var e = GetEntity(TransformUsageFlags.Renderable);

                float baseLen = Mathf.Max(0.001f, a.BaseLength);

                // Если включено автоматическое измерение, вычисляем длину по габаритам рендереров.
                if (a.AutoMeasureBounds)
                {
                    var renderers = a.GetComponentsInChildren<Renderer>();
                    if (renderers != null && renderers.Length > 0)
                    {
                        var b = renderers[0].bounds;
                        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

                        var axis = a.LengthAxis.normalized;
                        var size = b.size;
                        // Проецируем размер габаритного контейнера на указанную ось, чтобы найти длину.
                        float projectX = Mathf.Abs(Vector3.Dot(axis, Vector3.right));
                        float projectY = Mathf.Abs(Vector3.Dot(axis, Vector3.up));
                        float projectZ = Mathf.Abs(Vector3.Dot(axis, Vector3.forward));
                        baseLen = projectX > projectY && projectX > projectZ ? size.x :
                                  projectY > projectZ ? size.y : size.z;

                        baseLen = Mathf.Max(0.001f, baseLen);
                    }
                }

                // Добавляем компонент с настройками сегмента.
                AddComponent(e, new ConveyorSegmentSettings
                {
                    Length = baseLen,
                    MinLength = Mathf.Max(0.001f, a.MinLength),
                    MaxLength = Mathf.Max(a.MinLength, a.MaxLength),
                    ItemsPerMinute = Mathf.Max(1f, a.ItemsPerMinute),
                    Speed = Mathf.Max(0.1f, a.SpeedMetersPerSecond)
                });

                // Добавляем компонент с настройками энергопотребления.
                AddComponent(e, new ConveyorEnergySettings
                {
                    Value = Mathf.Max(0f, a.EnergyConsumptionPerSegmentKW)
                });
            }
        }
    }
}