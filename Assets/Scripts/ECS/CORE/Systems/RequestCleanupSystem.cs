using Unity.Entities;
using Unity.Collections;

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
                ComponentType.ReadWrite<CloseAllUIRequest>(),
                ComponentType.ReadWrite<InteractionRequest>(),
                ComponentType.ReadWrite<HireNPCRequest>(),
                ComponentType.ReadWrite<AssignNPCToTaskRequest>(),
                ComponentType.ReadWrite<AddItemRequest>(),
                ComponentType.ReadWrite<RemoveItemRequest>()
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