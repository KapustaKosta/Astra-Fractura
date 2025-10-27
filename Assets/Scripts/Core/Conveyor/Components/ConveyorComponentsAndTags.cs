using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace Conveyor
{
    /// <summary>
    /// Компонент-тег, указывающий, что игра находится в режиме строительства конвейеров.
    /// </summary>
    public struct InConveyorMode : IComponentData { }

    /// <summary>
    /// Содержит состояние текущего процесса строительства конвейера.
    /// </summary>
    public struct ConveyorState : IComponentData
    {
        public Entity PreviewEntity; // Сущность для предпросмотра конвейера
        public bool HasStart; // Установлена ли начальная точка
        public Entity StartConnector; // Коннектор начальной точки
        public int SegmentsLocked; // Количество зафиксированных сегментов
        public float SnapRadius; // Радиус привязки к коннекторам
        public int ItemID; // ID предмета (типа конвейера), который строится
    }

    /// <summary>
    /// Хранит имя здания в виде строки фиксированной длины.
    /// </summary>
    public struct BuildingName : IComponentData
    {
        public FixedString64Bytes Value;
    }

    /// <summary>
    /// Перечисление, определяющее тип коннектора конвейера: вход, выход или двунаправленный.
    /// </summary>
    public enum ConveyorConnectorType : byte { In = 0, Out = 1, Bidirectional = 2 }

    /// <summary>
    /// Определяет точку подключения для конвейерной ленты на здании или другом объекте.
    /// </summary>
    public struct ConveyorConnector : IComponentData
    {
        public ConveyorConnectorType Type; // Тип коннектора (вход/выход)
        public Entity Owner; // Сущность-владелец этого коннектора
        public float3 LocalPosition; // Локальная позиция относительно владельца
        public Entity ConnectedSegment; // Сегмент конвейера, подключенный к этому коннектору
    }

    /// <summary>
    /// Компонент-тег для обозначения подсвеченного коннектора.
    /// </summary>
    public struct ConveyorConnectorHighlighted : IComponentData { }

    /// <summary>
    /// Компонент-тег, указывающий, что курсор наведен на коннектор.
    /// </summary>
    public struct HoveredConnectorTag : IComponentData { }

    /// <summary>
    /// Содержит основные настройки для сегмента конвейера.
    /// </summary>
    public struct ConveyorSegmentSettings : IComponentData
    {
        public float Length; // Длина по умолчанию
        public float MinLength; // Минимально допустимая длина
        public float MaxLength; // Максимально допустимая длина
        public float ItemsPerMinute; // Пропускная способность (предметов в минуту)
        public float Speed; // Скорость движения предметов по конвейеру
    }

    /// <summary>
    /// Компонент-тег для сущностей, являющихся частью предпросмотра строящегося конвейера.
    /// </summary>
    public struct ConveyorPreviewTag : IComponentData { }

    /// <summary>
    /// Компонент-тег для "призрачных" (полупрозрачных) объектов предпросмотра конвейера.
    /// </summary>
    public struct ConveyorGhostTag : IComponentData { }

    /// <summary>
    /// Компонент-тег, указывающий, что коннектор или область заняты.
    /// </summary>
    public struct ConveyorOccupiedTag : IComponentData { }

    /// <summary>
    /// Хранит рантайм-данные для системы предпросмотра конвейера.
    /// </summary>
    public struct ConveyorPreviewRuntime : IComponentData
    {
        public int LastTailCount; // Последнее известное количество "хвостовых" сегментов
        public float LastTailPerLen; // Последняя известная длина каждого "хвостового" сегмента
    }

    /// <summary>
    /// Буферный элемент, хранящий точку пути конвейера с флагом блокировки.
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct ConveyorPathPoint : IBufferElementData
    {
        public float3 Position; public byte IsLocked;
    }

    /// <summary>
    /// Буферный элемент, хранящий путевую точку (вейпоинт) для движения по конвейеру.
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct ConveyorWaypoint : IBufferElementData
    {
        public float3 Position;
    }

    /// <summary>
    /// Буферный элемент, хранящий точку пути, используемую в процессе строительства конвейера.
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct ConveyorBuildPathPoint : IBufferElementData
    {
        public float3 Position;
    }

    /// <summary>
    /// Буферный элемент для хранения "замороженных" поз (положения, вращения, длины) сегментов призрачного конвейера.
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct ConveyorFrozenPose : IBufferElementData
    {
        public float3 Position; 
        public quaternion Rotation; 
        public float Length;
    }

    /// <summary>
    /// Буферный элемент для хранения "живых" (обновляемых) поз сегментов призрачного конвейера.
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct ConveyorLivePose : IBufferElementData
    {
        public float3 Position; 
        public quaternion Rotation; 
        public float Length;
    }

    /// <summary>
    /// Буферный элемент для хранения ссылок на "замороженные" сущности-призраки конвейера.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct ConveyorGhostFrozenRef : IBufferElementData
    {
        public Entity Value;
    }

    /// <summary>
    /// Буферный элемент для хранения ссылок на "живые" сущности-призраки конвейера.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct ConveyorGhostLiveRef : IBufferElementData
    {
        public Entity Value;
    }

    /// <summary>
    /// Компонент-запрос на вход в режим строительства конвейеров.
    /// </summary>
    public struct EnterConveyorModeRequest : IComponentData
    {
        public int ItemID;
    }

    /// <summary>
    /// Компонент-запрос на выход из режима строительства конвейеров.
    /// </summary>
    public struct ExitConveyorModeRequest : IComponentData
    {
        
    }

    /// <summary>
    /// Компонент-запрос на подтверждение и размещение построенного конвейера.
    /// </summary>
    public struct ConfirmConveyorPlacementRequest : IComponentData
    {
        public int ItemID; 
        public Entity PreviewHolder; 
        public Entity StartConnector; 
        public Entity EndConnector;
    }

    /// <summary>
    /// Компонент-запрос на постройку конвейера на основе существующего пути.
    /// </summary>
    public struct ConveyorBuildFromPathRequest : IComponentData
    {
        public Entity StartConnector; 
        public Entity EndConnector; 
        public int ItemID; 
        public Entity PreviewHolder;
    }

    /// <summary>
    /// Компонент-запрос на обновление состояния коннекторов после завершения строительства.
    /// </summary>
    public struct PostBuildConnectorUpdateRequest : IComponentData
    {
        public Entity StartConnector; 
        public Entity EndConnector;
    }

    /// <summary>
    /// Буферный элемент, содержащий ссылки на только что построенные сегменты конвейера.
    /// </summary>
    [InternalBufferCapacity(64)]
    public struct NewlyBuiltConveyorSegmentRef : IBufferElementData
    {
        public Entity Value;
    }

    /// <summary>
    /// Компонент-запрос на перерасчет маршрутов для всей конвейерной сети, связанной с указанным зданием.
    /// </summary>
    public struct RecalculateRoutesForNetworkRequest : IComponentData
    {
        public Entity SourceBuilding;
    }

    /// <summary>
    /// Хранит масштаб сегмента конвейера по оси Z, который определяет его длину.
    /// </summary>
    public struct ConveyorSegmentScale : IComponentData
    {
        public float Z; // масштаб по оси Z (отношение фактической длины к базовой)
    }

    /// <summary>
    /// Компонент-тег, указывающий, что текущее размещение конвейера является допустимым.
    /// </summary>
    public struct ConveyorPlacementValidTag : IComponentData { }
    
    /// <summary>
    /// Фактическая длина конкретного экземпляра сегмента конвейера (после масштабирования).
    /// Записывается при строительстве и используется визуализацией/таймингами.
    /// </summary>
    public struct ConveyorSegmentRuntimeLength : IComponentData
    {
        public float Value;
    }

    /// <summary>
    /// Настройки энергопотребления для префаба конвейера.
    /// </summary>
    public struct ConveyorEnergySettings : IComponentData
    {
        public float Value; // кВт на один сегмент
    }

    /// <summary>
    /// Расчетное полное энергопотребление для конкретного маршрута конвейера.
    /// </summary>
    public struct ConveyorEnergyDemand : IComponentData
    {
        public float RequiredKW;
    }

    /// <summary>
    /// Состояние энергоснабжения маршрута конвейера.
    /// </summary>
    public struct RoutePowerStatus : IComponentData
    {
        public float PowerRatio; // 0.0 (нет энергии) .. 1.0 (полное снабжение)
    }
}