using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Построитель контекста оценки целей ИИ.
/// Используется для создания экземпляра GoalEvaluationContext с необходимыми зависимостями.
/// </summary>
public class GoalEvaluationContextBuilder
{
    private readonly EntityManager _entityManager;
    private AISettings _settings;
    private Entity _settlementEntity;
    private CollisionWorld _collisionWorld;
    private ComponentLookup<ResourceNode> _resourceNodeLookup;
    private ComponentLookup<LocalToWorld> _transformLookup;
    private BufferLookup<InventoryItemElement> _inventoryLookup;
    private ResourceItemMapping _resourceItemMap;

    /// <summary>
    /// Инициализирует новый экземпляр класса GoalEvaluationContextBuilder.
    /// Получает доступ к системным компонентам и буферам.
    /// </summary>
    /// <param name="system">Система ECS для получения lookup'ов</param>
    public GoalEvaluationContextBuilder(SystemBase system)
    {
        _entityManager = system.EntityManager;
        _resourceNodeLookup = system.GetComponentLookup<ResourceNode>(true);
        _transformLookup = system.GetComponentLookup<LocalToWorld>(true);
        _inventoryLookup = system.GetBufferLookup<InventoryItemElement>(true);
    }

    /// <summary>
    /// Устанавливает настройки AI для контекста.
    /// </summary>
    /// <param name="settings">Настройки AI</param>
    /// <returns>Текущий экземпляр билдера для цепочки вызовов</returns>
    public GoalEvaluationContextBuilder WithSettings(AISettings settings)
    {
        _settings = settings;
        return this;
    }

    /// <summary>
    /// Устанавливает физический мир для контекста.
    /// </summary>
    /// <param name="physicsWorld">Физический мир из PhysicsWorldSingleton</param>
    /// <returns>Текущий экземпляр билдера для цепочки вызовов</returns>
    public GoalEvaluationContextBuilder WithPhysicsWorld(PhysicsWorldSingleton physicsWorld)
    {
        _collisionWorld = physicsWorld.CollisionWorld;
        return this;
    }

    /// <summary>
    /// Устанавливает сущность поселения для контекста.
    /// </summary>
    /// <param name="settlementEntity">Сущность поселения</param>
    /// <returns>Текущий экземпляр билдера для цепочки вызовов</returns>
    public GoalEvaluationContextBuilder WithSettlement(Entity settlementEntity)
    {
        _settlementEntity = settlementEntity;
        return this;
    }

    /// <summary>
    /// Устанавливает маппинг ресурсов в предметы для контекста.
    /// </summary>
    /// <param name="resourceItemMapping">Экземпляр ResourceItemMapping</param>
    /// <returns>Текущий экземпляр билдера для цепочки вызовов</returns>
    public GoalEvaluationContextBuilder WithManagedDependencies(ResourceItemMapping resourceItemMapping)
    {
        _resourceItemMap = resourceItemMapping;
        return this;
    }

    /// <summary>
    /// Создаёт и возвращает экземпляр GoalEvaluationContext.
    /// Проверяет наличие всех обязательных зависимостей.
    /// </summary>
    /// <returns>Построенный контекст оценки целей</returns>
    public GoalEvaluationContext Build()
    {
        // Проверяем обязательные зависимости
        if (_settings.Equals(default(AISettings)) || 
            _settlementEntity == Entity.Null || 
            _resourceItemMap == null)
        {
            #if UNITY_EDITOR
            Debug.LogError("[GoalEvaluationContextBuilder] Не все обязательные зависимости были предоставлены!");
            #endif
        }

        return new GoalEvaluationContext(
            _entityManager,
            _settings,
            _settlementEntity,
            _collisionWorld,
            _resourceNodeLookup,
            _transformLookup,
            _inventoryLookup,
            _resourceItemMap
        );
    }
}