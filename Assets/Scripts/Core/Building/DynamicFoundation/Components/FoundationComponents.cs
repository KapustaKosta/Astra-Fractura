using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Тег-компонент для фундаментов, хранящий размер их сетки для привязки.
/// </summary>
public struct FoundationTag : IComponentData
{
    public float2 GridSize;
}

/// <summary>
/// Хранит высоту плитки фундамента (опционально, для других систем).
/// </summary>
public struct FoundationTileHeight : IComponentData
{
    public float Value;
}

/// <summary>
/// Маркер, что для объекта нужен динамический фундамент (опоры).
/// Хранит ссылку на префаб элемента фундамента.
/// </summary>
public struct RequiresDynamicFoundation : IComponentData
{
    public Entity FoundationPrefab;
}

/// <summary>
/// Целевой масштаб фундамента (W,H,D). По нему пересобирается BoxCollider.
/// </summary>
public struct FoundationScale : IComponentData
{
    public float3 Value;
}

/// <summary>
/// Габариты здания по XZ, выпекаются из рендеров префаба.
/// </summary>
public struct BuildingFootprint : IComponentData
{
    public float2 Size;
}

/// <summary>
/// Синглтон для временного хранения параметров высоты превью в момент клика.
/// </summary>
public struct FoundationPlacementSnapshot : IComponentData
{
    public float ScaleY;          // scale превью по Y на момент клика (если нет таргета)
    public float TotalHeight;     // общая высота превью (от effectiveGroundY до верха)
    public float3 ExpectedPos;    // ожидаемая позиция пивота финального энтити на момент клика
    public byte HasData;          // 0/1

    // Новое: если во время клика мы были примагничены к соседней палубе, сохраняем целевую DeckWorldY
    public byte HasTargetDeckY;   // 0/1
    public float TargetDeckY;     // абсолютная высота верха палубы, к которой надо подогнать
}

/// <summary>
/// Минимальный зазор между верхом фундамента и землёй (метры).
/// </summary>
public struct FoundationClearance : IComponentData
{
    public float Value;
}

/// <summary>
/// Компонент-запрос на изменение высоты превью здания.
/// </summary>
public struct AdjustBuildingHeightRequest : IComponentData
{
    public float ScrollDelta;
}

/// <summary>
/// Хранит текущее пользовательское смещение высоты превью здания относительно земли.
/// </summary>
public struct BuildingHeightOffset : IComponentData
{
    public float Value;
}

/// <summary>
/// Компонент на финальном здании, хранящий данные о его "поле" для снэппинга.
/// </summary>
public struct FoundationDeck : IComponentData
{
    /// <summary>Высота верхней плоскости палубы (мировая Y).</summary>
    public float DeckWorldY;

    /// <summary>Центр палубы в XZ (мировые координаты).</summary>
    public float2 CenterXZ;

    /// <summary>Размер палубы по XZ (м).</summary>
    public float2 SizeXZ;

    /// <summary>Ориентация палубы (мировой поворот). Используется для ротационно-корректного снэпа.</summary>
    public quaternion Orientation;
}

/// <summary>
/// Компонент на главном превью, хранящий ссылку на связанное с ним превью фундамента.
/// </summary>
public struct BuildingPreviewLink : IComponentData
{
    public Entity FoundationPreviewEntity;
}

/// <summary>
/// Компонент на главном превью, хранящий Y-координату земли под курсором.
/// </summary>
public struct PreviewGroundPosition : IComponentData
{
    public float GroundY;
}

/// <summary>
/// Маркер «нужно запечь коллайдер» с заданным масштабом по Y.
/// </summary>
public struct FoundationColliderScale : IComponentData
{
    public float Y; // масштаб по оси Y, который надо запечь в коллайдер
}

/// <summary>
/// Состояние «магнита» по высоте для превью фундамента.
/// Если активен, TargetDeckY — высота палубы, к которой мы защёлкнулись.
/// </summary>
public struct PreviewHeightSnapState : IComponentData
{
    public float TargetDeckY; // мировая Y верхней палубы-соседа
    public byte IsActive;     // 0/1 — сейчас примагничены
}
