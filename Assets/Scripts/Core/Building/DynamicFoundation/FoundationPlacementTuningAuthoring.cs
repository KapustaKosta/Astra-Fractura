using Unity.Entities;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("ECS/Building/Foundation Placement Tuning")]
public sealed class FoundationPlacementTuningAuthoring : MonoBehaviour
{
    [Header("Окружение / радиусы")]
    [Tooltip("Максимальная дистанция по XZ, в пределах которой высота превью прилипает к соседней палубе (м).")]
    public float HeightSnapMaxDist = 6.0f;

    [Tooltip("Радиус сопоставления при финализации (по XZ) — чтобы итоговая высота тоже подмагничивалась.")]
    public float PlaceMatchMaxDist = 6.0f;

    [Tooltip("На сколько метров фундамент уходит ниже земли (для скрытия щелей на склонах).")]
    public float SinkAmount = 0.10f;

    [Header("Скролл/магнит по тикам")]
    [Tooltip("Сколько ТИКОВ колеса от целевой высоты нужно для автопривязки. 2 = ±2 щелчка.")]
    public int HeightSnapTicks = 2;

    [Tooltip("Высота, приходящаяся на 1 щелчок колеса (в метрах). 0 = автоопределение по фактическому сдвигу BuildingHeightOffset.")]
    public float HeightScrollStepMeters = 0.0f;

    [Tooltip("Допуск к окну снапа (м). Если 0 — берётся 25% от шага скролла.")]
    public float HeightSnapEpsilonMeters = 0.0f;

    public sealed class Baker : Unity.Entities.Baker<FoundationPlacementTuningAuthoring>
    {
        public override void Bake(FoundationPlacementTuningAuthoring src)
        {
            var e = GetEntity(TransformUsageFlags.None);
            AddComponent(e, new FoundationPlacementTuning
            {
                HeightSnapMaxDist = Mathf.Max(0f, src.HeightSnapMaxDist),
                PlaceMatchMaxDist = Mathf.Max(0f, src.PlaceMatchMaxDist),
                SinkAmount = Mathf.Max(0f, src.SinkAmount),
                HeightSnapTicks = Mathf.Max(1, src.HeightSnapTicks),
                HeightScrollStep = Mathf.Max(0f, src.HeightScrollStepMeters),
                HeightSnapEpsilon = Mathf.Max(0f, src.HeightSnapEpsilonMeters),
            });
        }
    }
}

/// <summary>Singleton-тюнинг размещения фундаментов.</summary>
public struct FoundationPlacementTuning : IComponentData
{
    public float HeightSnapMaxDist;   // радиус по XZ для высотного «магнита»
    public float PlaceMatchMaxDist;   // радиус финальной подгонки
    public float SinkAmount;          // утопление под землю
    public int HeightSnapTicks;     // ±N тиков для окна снапа
    public float HeightScrollStep;    // шаг высоты на один тик (0 = авто)
    public float HeightSnapEpsilon;   // дополнительный метрический допуск к окну (0 = 25% от шага)
}
