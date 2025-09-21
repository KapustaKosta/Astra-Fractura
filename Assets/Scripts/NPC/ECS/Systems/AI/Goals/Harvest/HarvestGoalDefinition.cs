using Unity.Entities;
using Unity.Physics;
using UnityEngine;

/// <summary>
/// Определение цели "Сбор ресурсов" для ИИ.
/// Создает экземпляр через меню Unity и очищает связанные компоненты при смене цели.
/// </summary>
[CreateAssetMenu(fileName = "Goal_Harvest", menuName = "AI/Goal Definitions/Harvest")]
[Cleanup(typeof(AIActiveTarget), typeof(WantsToHarvestTag), typeof(MovementFailedTag))]
public class HarvestGoalDefinition : GoalDefinition
{
    /// <summary>
    /// Проверяет, может ли сущность рассматривать эту цель.
    /// Цель недоступна, если NPC заблокирован (метка HarvestingBlockedTag).
    /// </summary>
    public override bool CanBeConsidered(Entity entity, in GoalEvaluationContext context)
    {
        bool isBlocked = context.EntityManager.HasComponent<HarvestingBlockedTag>(entity);
        return !isBlocked; 
    }

    /// <summary>
    /// Возвращает базовый балл для цели. 
    /// Базовый расчет без дополнительных факторов.
    /// </summary>
    public override float ScoreGoal(Entity entity, in GoalEvaluationContext context)
    {
        return BaseScore;
    }

    /// <summary>
    /// Создает активную цель сбора ресурсов.
    /// Находит ближайший ресурс и проверяет его соответствие предмету.
    /// </summary>
    public override ActiveGoal CreateGoal(Entity entity, in GoalEvaluationContext context, float score)
    {
        // Получаем позицию NPC
        var npcTransform = context.TransformLookup[entity];
        
        // Фильтр для поиска ресурсов
        var resourceFilter = new CollisionFilter
        {
            BelongsTo = ~0u,
            CollidesWith = (uint)(1 << context.Settings.ResourceCollisionLayer),
            GroupIndex = 0
        };

        // Поиск ближайшего ресурса
        Entity nearestResource = AIPhysicsQuery.FindNearestResource(
            npcTransform.Position,
            context.Settings.AISearchRadius,
            in context.CollisionWorld,
            resourceFilter,
            in context.ResourceNodeLookup,
            in context.TransformLookup
        );

        if (nearestResource == Entity.Null)
        {
            return default;
        }
        
        // Получаем тип ресурса
        var resourceNode = context.ResourceNodeLookup[nearestResource];
        
        // Преобразуем тип ресурса в ItemID
        if (context.ResourceItemMap.TryGetItemID(resourceNode.resourceType, out int itemID))
        {
            return new ActiveGoal
            {
                Type = this.Type,
                Target = nearestResource,
                RelevantItemID = itemID,
                CurrentGoalScore = score
            };
        }
        return default;
    }
}