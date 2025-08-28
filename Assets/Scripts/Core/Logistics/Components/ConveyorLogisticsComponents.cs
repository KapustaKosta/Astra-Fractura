using Unity.Entities;

namespace Conveyor
{
    /// <summary>
    /// Основной компонент, определяющий маршрут. Хранит начальную и конечную точки,
    /// а также тип предмета, который по нему перемещается.
    /// </summary>
    public struct RouteDefinition : IComponentData
    {
        public Entity StartConnector;
        public Entity EndConnector;
        public int ItemID;
        public float ThroughputPerMinute; // Предметов в минуту
        public int TransferBatchSize;     // Размер одного стака для передачи
    }

    /// <summary>
    /// Элемент буфера, хранящий одну сущность сегмента конвейера.
    /// Последовательность элементов в буфере определяет путь.
    /// </summary>
    [InternalBufferCapacity(128)]
    public struct RoutePathElement : IBufferElementData
    {
        public Entity SegmentEntity;
    }

    /// <summary>
    /// Компонент-ссылка на сегменте, указывающий, к какому логическому маршруту он принадлежит.
    /// </summary>
    public struct BelongsToRoute : IComponentData
    {
        public Entity RouteEntity;
    }

    /// <summary>
    /// Создает "связный список" из сегментов. Каждый сегмент знает, какой идет после него.
    /// </summary>
    public struct ConveyorLink : IComponentData
    {
        public Entity NextSegment;
    }

    /// <summary>
    /// Компонент-таймер на сущности маршрута. Управляет частотой отправки предметов.
    /// </summary>
    public struct RouteTimer : IComponentData
    {
        public float Cooldown;
        public float TimeToNextTransfer;
    }

    /// <summary>
    /// Компонент-флаг на сущности маршрута, указывающий, что он активен и готов к работе.
    /// </summary>
    public struct ActiveRouteTag : IComponentData { }

    /// <summary>
    /// Сущность-"посылка". Представляет предмет, который находится в пути по конвейеру.
    /// </summary>
    public struct ItemInTransit : IComponentData
    {
        public Entity RouteEntity;
        public int ItemID;
        public int Amount;
        public Entity DestinationInventory;
        public float StartTime;
        public float TravelDuration;
    }

    /// <summary>
    /// Тег для ВИЗУАЛЬНОГО ПРЕДСТАВЛЕНИЯ предмета на ленте (кубика).
    /// Вешается на префаб и на созданные экземпляры кубиков через Authoring.
    /// </summary>
    public struct ItemVisualTag : IComponentData { }

    /// <summary>
    /// Тег, который вешается на ЛОГИЧЕСКУЮ сущность ItemInTransit,
    /// когда для нее уже был создан визуальный объект.
    /// </summary>
    public struct HasVisualTag : IComponentData { }

    /// <summary>
    /// Компонент, который вешается на ВИЗУАЛЬНЫЙ объект (кубик) и ссылается
    /// на ЛОГИЧЕСКУЮ сущность-посылку, за которой он следует.
    /// </summary>
    public struct VisualFor : IComponentData
    {
        public Entity LogicalEntity;
    }

    /// <summary>
    /// Запрос на поиск и создание нового маршрута.
    /// </summary>
    public struct DiscoverRouteRequest : IComponentData
    {
        public Entity StartConnector;
        public Entity EndConnector;
    }

    /// <summary>
    /// Внутренний тег-маркер, который откладывает обработку запроса на один кадр.
    /// </summary>
    public struct AwaitingDiscoveryTag : IComponentData { }

    /// <summary>
    /// Запрос на открытие UI-панели управления всеми конвейерными маршрутами.
    /// </summary>
    public struct OpenConveyorRoutesUIRequest : IComponentData { }

    /// <summary>
    /// Запрос на установку конкретного типа предмета для транспортировки по маршруту.
    /// </summary>
    public struct SetRouteItemRequest : IComponentData
    {
        public Entity RouteEntity;
        public int NewItemID;
    }

    /// <summary>
    /// Запрос на переключение состояния маршрута (активен/пауза).
    /// </summary>
    public struct ToggleRouteRequest : IComponentData
    {
        public Entity RouteEntity;
    }
}