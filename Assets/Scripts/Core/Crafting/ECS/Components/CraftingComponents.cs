using Unity.Entities;

/// <summary>
/// Представляет один элемент в очереди на создание предметов.
/// Динамический буфер (DynamicBuffer) из этих элементов, прикрепленный к сущности (например, игроку),
/// формирует его персональную очередь крафта.
/// </summary>
public struct CraftingQueueElement : IBufferElementData
{
    /// <summary>
    /// Оставшееся время в секундах до завершения создания этого предмета.
    /// </summary>
    public float TimeRemaining;

    /// <summary>
    /// Общее время, необходимое для создания этого предмета.
    /// Используется для расчета прогресса в UI 
    /// </summary>
    public float TotalCraftingTime;

    /// <summary>
    /// Уникальный идентификатор (ID) предмета, который будет создан.
    /// </summary>
    public int ResultItemID;

    /// <summary>
    /// Количество предметов, которое будет создано по завершении.
    /// </summary>
    public int ResultAmount;
}

/// <summary>
/// Одноразовая сущность-запрос, инициирующая процесс крафта.
/// Создается UI и обрабатывается системой CraftingProcessingSystem, после чего удаляется.
/// </summary>
public struct StartCraftingRequest : IComponentData
{
    /// <summary>
    /// Сущность, которая выполняет крафт (например, игрок или станок).
    /// </summary>
    public Entity Crafter;

    /// <summary>
    /// Уникальный идентификатор (ID) итогового предмета.
    /// </summary>
    public int ResultItemID;

    /// <summary>
    /// Количество создаваемых предметов.
    /// </summary>
    public int ResultAmount;

    /// <summary>
    /// Общее время в секундах, необходимое на крафт.
    /// </summary>
    public float CraftingTime;
}

/// <summary>
/// Представляет один тип ресурса, необходимый для крафта.
/// Динамический буфер из этих элементов прикрепляется к сущности-запросу StartCraftingRequest,
/// формируя полный список требуемых ингредиентов.
/// </summary>
public struct RequiredCraftingItem : IBufferElementData
{
    /// <summary>
    /// Уникальный идентификатор (ID) требуемого предмета-ингредиента.
    /// </summary>
    public int ItemID;

    /// <summary>
    /// Требуемое количество этого предмета.
    /// </summary>
    public int Amount;
}