using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Компонент конвеера, содержит информацию о направлении и состоянии.
/// </summary>
public struct ConveyorComponent : IComponentData
{
    public Entity StartEntity; // Сущность-источник (выход здания или другой конвеер)
    public Entity EndEntity;   // Сущность-приёмник (вход здания или другой конвеер)
    public float3 Direction;   // Направление движения ресурсов
    public float Length;       // Длина конвеера
}

/// <summary>
/// Тег для сущности-конвеера.
/// </summary>
public struct ConveyorBeltTag : IComponentData { }

/// <summary>
/// Буфер для хранения ресурсов, находящихся на конвеере.
/// </summary>
public struct ConveyorResourceBuffer : IBufferElementData
{
    public Entity ResourceEntity;
    public float Progress; // 0..1, насколько далеко ресурс продвинулся по ленте
}

/// <summary>
/// Компонент точки подключения (вход/выход здания).
/// </summary>
/// <summary>
/// Компонент точки подключения конвеера (отдельная ECS-сущность).
/// </summary>
public struct ConveyorEndpoint : IComponentData
{
    public Entity ParentEntity; // Ссылка на родительское здание или конвеер
    public bool IsInput;        // true - вход, false - выход
    public EndpointType Type;   // Тип точки (например, вход, выход, промежуточная)
}

/// <summary>
/// Тип точки подключения конвеера.
/// </summary>
public enum EndpointType : byte
{
    Input = 0,
    Output = 1,
    Intermediate = 2
}
