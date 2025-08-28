using Unity.Entities;
using Unity.Collections;
using Game.Workshop;
using Wiring;
using Conveyor;
using Energy.Core;

/// <summary>
/// Универсальная система, которая выполняется в конце кадра и удаляет все сущности,
/// имеющие компонент-запрос, помеченный интерфейсом IRequestCleanup.
/// Это обеспечивает автоматическую очистку одноразовых запросов.
/// </summary>
[UpdateInGroup(typeof(LateSimulationSystemGroup))]
public partial class RequestCleanupSystem : SystemBase
{
    private EntityQuery m_RequestQuery;

    /// <summary>
    /// Вызывается при создании системы. Определяет EntityQuery для поиска всех сущностей с компонентами-запросами.
    /// </summary>
    protected override void OnCreate()
    {
        var queryDesc = new EntityQueryDesc
        {
            Any = new ComponentType[]
            {
                ComponentType.ReadWrite<EnterBuildingModeRequest>(),
                ComponentType.ReadWrite<ExitBuildingModeRequest>(),
                ComponentType.ReadWrite<PlaceBuildingRequest>(),
                ComponentType.ReadWrite<ToggleInventoryRequest>(),
                ComponentType.ReadWrite<OpenNPCUIRequest>(),
                ComponentType.ReadWrite<OpenSettlementUIRequest>(),
                ComponentType.ReadWrite<OpenTradeUIRequest>(),
                ComponentType.ReadWrite<CloseAllUIRequest>(),
                ComponentType.ReadWrite<InteractionRequest>(),
                ComponentType.ReadWrite<HireNPCRequest>(),
                ComponentType.ReadWrite<AddItemRequest>(),
                ComponentType.ReadWrite<RemoveItemRequest>(),
                ComponentType.ReadWrite<MoveItemRequest>(),
                ComponentType.ReadWrite<SplitStackRequest>(),
                ComponentType.ReadWrite<PlayerAssignHarvestRequest>(),
                ComponentType.ReadWrite<OpenProductionUIRequest>(),
                ComponentType.ReadWrite<PerformAttackRequest>(),
                ComponentType.ReadWrite<OpenWorkshopUIRequest>(),
                ComponentType.ReadWrite<EnterWirePlacementModeRequest>(),
                ComponentType.ReadWrite<ExitWirePlacementModeRequest>(),
                ComponentType.ReadWrite<EnterConveyorModeRequest>(),
                ComponentType.ReadWrite<ExitConveyorModeRequest>(),
                ComponentType.ReadWrite<OpenGeneratorUIRequest>(),
                ComponentType.ReadWrite<OpenBatteryUIRequest>(),
                ComponentType.ReadWrite<ToggleGeneratorRequest>(),
                ComponentType.ReadWrite<SetRouteItemRequest>(),
                ComponentType.ReadWrite<ToggleRouteRequest>(),
                ComponentType.ReadWrite<OpenConveyorRoutesUIRequest>(),
                ComponentType.ReadWrite<ConfirmConveyorPlacementRequest>(),
                ComponentType.ReadWrite<RemoveConveyorUnderCursorRequest>()
            }
        };
        m_RequestQuery = GetEntityQuery(queryDesc);
    }

    /// <summary>
    /// Вызывается каждый кадр. Уничтожает все сущности, соответствующие запросу m_RequestQuery.
    /// </summary>
    protected override void OnUpdate()
    {
        EntityManager.DestroyEntity(m_RequestQuery);
    }
}