using Unity.Entities;

namespace Wiring
{
    /// <summary>Запрос на отмену текущей прокладки провода.</summary>
    public struct CancelWirePlacementRequest : IComponentData { }

    /// <summary>
    /// Запрос на удаление провода под курсором.
    /// Обрабатывается отдельной системой, которая ищет ближайший к лучу сегмент.
    /// </summary>
    public struct RemoveWireUnderCursorRequest : IComponentData { }
}
