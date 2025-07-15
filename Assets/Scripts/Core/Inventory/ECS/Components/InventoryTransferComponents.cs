using Unity.Entities;

/// <summary>
/// Одноразовый запрос на атомарную передачу предметов из одного инвентаря в другой.
/// Обрабатывается системой InventoryTransferSystem.
/// </summary>
public struct TransferItemsRequest : IComponentData
{
    public Entity SourceOwner;
    public Entity DestinationOwner;
}

/// <summary>
/// Тег, добавляемый к сущности-источнику после успешного завершения TransferItemsRequest.
/// </summary>
public struct TransferSuccessTag : IComponentData { }

/// <summary>
/// Тег, добавляемый к сущности-источнику, если TransferItemsRequest не удалось выполнить
/// (например, из-за нехватки места в инвентаре-получателе).
/// </summary>
public struct TransferFailedTag : IComponentData { }