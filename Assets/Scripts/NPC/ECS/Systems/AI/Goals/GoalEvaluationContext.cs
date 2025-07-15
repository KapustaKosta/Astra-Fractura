using Unity.Entities;
using Unity.Transforms;
using Unity.Physics;

/// <summary>
/// Структура-контейнер, передающая необходимые данные и Lookup-таблицы из системы
/// в методы GoalDefinition для оценки и создания цели. Является "глупым" контейнером.
/// </summary>
public readonly ref struct GoalEvaluationContext
{
    // Системные объекты
    public readonly EntityManager EntityManager;

    // Данные из синглтонов
    public readonly AISettings Settings;
    public readonly Entity SettlementEntity;
    public readonly CollisionWorld CollisionWorld;

    // Lookup-таблицы для доступа к компонентам
    public readonly ComponentLookup<ResourceNode> ResourceNodeLookup;
    public readonly ComponentLookup<LocalToWorld> TransformLookup;
    public readonly BufferLookup<InventoryItemElement> InventoryLookup;
    
    // Ссылки на управляемые объекты (требуют .WithoutBurst())
    public readonly ResourceItemMapping ResourceItemMap;

    /// <summary>
    /// Конструктор, который просто принимает все необходимые, уже полученные данные от вызывающей системы.
    /// </summary>
    public GoalEvaluationContext(
        EntityManager entityManager,
        AISettings settings,
        Entity settlementEntity,
        CollisionWorld collisionWorld,
        ComponentLookup<ResourceNode> resourceNodeLookup,
        ComponentLookup<LocalToWorld> transformLookup,
        BufferLookup<InventoryItemElement> inventoryLookup,
        ResourceItemMapping resourceItemMap)
    {
        EntityManager = entityManager;
        Settings = settings;
        SettlementEntity = settlementEntity;
        CollisionWorld = collisionWorld;
        ResourceNodeLookup = resourceNodeLookup;
        TransformLookup = transformLookup;
        InventoryLookup = inventoryLookup;
        ResourceItemMap = resourceItemMap;
    }
}