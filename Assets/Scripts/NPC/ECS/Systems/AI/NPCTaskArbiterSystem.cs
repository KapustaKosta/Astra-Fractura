using Unity.Collections;
using Unity.Entities;
using Unity.Physics;

/// <summary>
/// Система выбора целей для ИИ NPC.
/// Определяет, какую цель должен преследовать NPC на основе оценки приоритетов.
/// Обновляется в группе SimulationSystemGroup после HarvestConditionSystem.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(HarvestConditionSystem))]
public partial class NPCTaskArbiterSystem : SystemBase
{
    // Ссылка на реестр целей ИИ
    private GoalRegistrySystem _goalRegistry;
    // Запрос для выборки всех NPC
    private EntityQuery _npcQuery;

    /// <summary>
    /// Инициализирует систему, получая доступ к реестру целей и создавая запрос на выборку NPC.
    /// Требует наличия GoalRegistrySystem.Initialized и PlayerSettlementTag для работы.
    /// </summary>
    protected override void OnCreate()
    {
        // Получаем доступ к реестру целей
        _goalRegistry = World.GetOrCreateSystemManaged<GoalRegistrySystem>();
        // Требуем инициализации реестра и наличия поселения
        RequireForUpdate<GoalRegistrySystem.Initialized>(); 
        RequireForUpdate<PlayerSettlementTag>();
        // Создаем запрос для выборки NPC
        _npcQuery = GetEntityQuery(
            ComponentType.ReadOnly<NPCBrain>(), 
            ComponentType.ReadOnly<NPCHiredTag>());
    }

    /// <summary>
    /// Основной метод системы, выполняющий выбор целей для всех NPC.
    /// Оценивает доступные цели и устанавливает лучшую для каждого NPC.
    /// </summary>
    protected override void OnUpdate()
    {
        // Создаем командный буфер для изменений
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        // Получаем доступ к реестру целей
        var goalDefinitions = _goalRegistry.GoalDefinitionsMap;
        
        // Проверяем, есть ли зарегистрированные цели
        if (goalDefinitions.Count == 0)
        {
            ecb.Dispose();
            return;
        }

        // Получаем глобальные настройки AI
        var settings = SystemAPI.GetSingleton<AISettings>();
        
        // Проверяем корректность настроек
        if (settings.Equals(default(AISettings)))
        {
            ecb.Dispose();
            return;
        }
        
        // Создаем контекст оценки целей с необходимыми зависимостями
        var context = new GoalEvaluationContextBuilder(this)
            .WithSettings(settings)
            .WithPhysicsWorld(SystemAPI.GetSingleton<PhysicsWorldSingleton>())
            .WithSettlement(SystemAPI.GetSingletonEntity<PlayerSettlementTag>())
            .WithManagedDependencies(ResourceItemMapping.Instance)
            .Build();

        // Получаем список всех NPC
        var npcEntities = _npcQuery.ToEntityArray(Allocator.Temp);
        
        // Основной цикл оценки целей для каждого NPC
        foreach (var entity in npcEntities)
        {
            GoalDefinition bestGoalDef = null;
            float bestScore = -1f;

            // Перебираем все доступные цели
            foreach (var kvp in goalDefinitions)
            {
                var currentDef = kvp.Value;
                // Проверяем, доступна ли цель для этого NPC
                if (currentDef.CanBeConsidered(entity, in context))
                {
                    // Рассчитываем приоритет цели
                    float score = currentDef.ScoreGoal(entity, in context);
                    // Выбираем цель с максимальным приоритетом
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestGoalDef = currentDef;
                    }
                }
            }

            // Пропускаем, если не найдено подходящих целей
            if (bestGoalDef == null) continue;
            
            // Проверяем текущую цель NPC
            bool hasActiveGoal = SystemAPI.HasComponent<ActiveGoal>(entity);
            GoalType currentGoalType = hasActiveGoal ? 
                SystemAPI.GetComponent<ActiveGoal>(entity).Type : 
                (GoalType)(-1);

            // Если выбранная цель отличается от текущей
            if (bestGoalDef.Type != currentGoalType)
            {
                // Очищаем предыдущую цель, если она была
                if (hasActiveGoal)
                {
                    ecb.RemoveComponent<ActiveGoal>(entity);
                    ecb.AddComponent(entity, new CleanupGoalRequest { OldGoalType = currentGoalType });
                }
                
                // Создаем новую цель
                ActiveGoal newGoal = bestGoalDef.CreateGoal(entity, in context, bestScore);
                
                // Проверяем корректность созданной цели
                if (newGoal.Type == bestGoalDef.Type)
                {
                    // Устанавливаем новую цель
                    ecb.AddComponent(entity, newGoal);
                    
                    // Для цели добычи добавляем дополнительные компоненты
                    if (newGoal.Type == GoalType.Harvest)
                    {
                        ecb.AddComponent(entity, new AIActiveTarget { Value = newGoal.Target });
                    }
                }
            }
        }
        
        // Применяем изменения и очищаем память
        npcEntities.Dispose();
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}