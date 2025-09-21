using Unity.Entities;
using UnityEngine;

/// <summary>
/// Определение цели "Вернуться на базу" для ИИ.
/// Создается через меню Unity (AI/Goal Definitions/Return To Base).
/// Очищает связанные компоненты при смене цели.
/// </summary>
[CreateAssetMenu(fileName = "Goal_ReturnToBase", menuName = "AI/Goal Definitions/Return To Base")]
[Cleanup(typeof(UnloadRequestTag), typeof(MissingRequiredItemForReturnTag))]
public class ReturnToBaseGoalDefinition : GoalDefinition
{
    [Header("Настройки Возвращения")]
    [Tooltip("Бонус к оценке, который добавляется пропорционально заполненности инвентаря.")]
    public float FullnessBonus = 50f;

    /// <summary>
    /// Проверяет, может ли NPC рассматривать цель "Вернуться на базу".
    /// Цель доступна, если инвентарь полон и не заблокирован.
    /// </summary>
    public override bool CanBeConsidered(Entity entity, in GoalEvaluationContext context)
    {
        var entityManager = context.EntityManager;
        var inventory = context.InventoryLookup[entity];
        var itemRegistry = ItemRegistry.Instance;
        
        // Проверяем, заполнен ли инвентарь
        bool isFull = itemRegistry != null && InventoryUtils.IsInventoryFull(inventory, itemRegistry);
        // Проверяем блокировку разгрузки
        bool isBlocked = entityManager.HasComponent<UnloadingBlockedTag>(entity);

        
        return isFull && !isBlocked; 
    }
    
    /// <summary>
    /// Рассчитывает приоритет цели на основе заполненности инвентаря.
    /// Если инвентарь переполнен (NPCInventoryFullTag), применяется экстренный приоритет.
    /// </summary>
    public override float ScoreGoal(Entity entity, in GoalEvaluationContext context)
    {
        var inventory = context.InventoryLookup[entity];

        // Экстренная разгрузка при переполнении
        if (context.EntityManager.HasComponent<NPCInventoryFullTag>(entity))
        {
            float emergencyScore = BaseScore + context.Settings.PlayerAssignReturnPriority;
            return emergencyScore;
        }
        
        // Обычный расчет по заполненности инвентаря
        float fullnessPercentage = InventoryUtils.GetFullnessPercentage(inventory);
        return BaseScore + (FullnessBonus * fullnessPercentage);
    }
    
    /// <summary>
    /// Создает активную цель "Вернуться на базу".
    /// Цель связывается с первым предметом в инвентаре.
    /// </summary>
    public override ActiveGoal CreateGoal(Entity entity, in GoalEvaluationContext context, float score)
    {
        // Проверяем наличие поселения
        if (context.SettlementEntity == Entity.Null) return default;

        var inventory = context.InventoryLookup[entity];
        int firstItemID = InventoryUtils.GetFirstItemID(inventory);
        
        // Не создаем цель для пустого инвентаря
        if (firstItemID == 0)
        {
            return default;
        }

        return new ActiveGoal
        {
            Type = this.Type,
            Target = context.SettlementEntity,
            RelevantItemID = firstItemID,
            CurrentGoalScore = score
        };
    }
}