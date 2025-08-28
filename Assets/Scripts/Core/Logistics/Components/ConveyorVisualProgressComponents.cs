using Unity.Entities;

namespace Conveyor
{
    /// <summary>
    /// Прогресс вдоль маршрута (метры/юниты), скорость, кэши для быстрого продвижения.
    /// </summary>
    public struct ConveyorVisualProgress : IComponentData
    {
        public float Distance;          // накопленная дистанция от начала пути
        public float Speed;             // юнитов/сек
        public float TotalLength;       // суммарная длина маршрута

        public int SegmentIndex;      // текущий сегмент (кэш)
        public float SegmentStartDist;  // суммарная дистанция у начала текущего сегмента (кэш)
    }

    /// <summary>
    /// Enableable-тег: визуал проинициализирован начальными значениями и позой.
    /// Должен добавляться выключенным при спавне и включаться в Init.
    /// </summary>
    public struct ConveyorVisualInitializedTag : IComponentData, IEnableableComponent { }

        /// <summary>
        /// Простой флаг: визуалу нужна одноразовая инициализация пути/длины.
        /// Вариант А: без enableable, обычный tag-компонент.
        /// </summary>
        public struct ConveyorVisualNeedsInitTag : IComponentData { }
    
}
