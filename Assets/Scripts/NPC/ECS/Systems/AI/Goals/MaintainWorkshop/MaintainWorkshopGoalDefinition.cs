using Unity.Entities;
using UnityEngine;

/// <summary>
/// Определяет цель по обслуживанию назначенного цеха.
/// Эта цель имеет очень высокий приоритет, чтобы перебивать рутинные задачи.
/// </summary>
[CreateAssetMenu(fileName = "Goal_MaintainWorkshop", menuName = "AI/Goal Definitions/Maintain Workshop")]
[Cleanup(typeof(AIActiveTarget), typeof(HasMaintenanceTaskTag))] // Очищаем наш тег после выполнения
public class MaintainWorkshopGoalDefinition : GoalDefinition
{
    /// <summary>
    /// Проверяет, есть ли у NPC активная задача по обслуживанию цеха (проверяя тег).
    /// </summary>
    public override bool CanBeConsidered(Entity entity, in GoalEvaluationContext context)
    {
        // Просто проверяем тег, который выставила наша система-сенсор.
        return context.EntityManager.HasComponent<HasMaintenanceTaskTag>(entity);
    }

    /// <summary>
    /// Возвращает статичный ОЧЕНЬ ВЫСОКИЙ балл, чтобы эта задача всегда была в приоритете,
    /// когда она доступна.
    /// </summary>
    public override float ScoreGoal(Entity entity, in GoalEvaluationContext context)
    {
        // Это самая важная строка. Она гарантирует, что оценка будет ~1100.
        // Она переопределяет значение Base Score из инспектора.
        return context.Settings.PlayerAssignReturnPriority + 100f;
    }

    /// <summary>
    /// Создает цель, направленную на цех из основного компонента NPC.
    /// </summary>
    public override ActiveGoal CreateGoal(Entity entity, in GoalEvaluationContext context, float score)
    {
        // Цель берется из поля AssignedWorkshop, которое уже есть у NPC.
        if (!context.EntityManager.HasComponent<NPCComponent>(entity))
        {
            return default; // Безопасная проверка
        }

        var npcData = context.EntityManager.GetComponentData<NPCComponent>(entity);
        if (npcData.AssignedWorkshop == Entity.Null)
        {
            return default; // У NPC нет назначенного цеха
        }

        return new ActiveGoal
        {
            Type = this.Type,
            Target = npcData.AssignedWorkshop,
            CurrentGoalScore = score
        };
    }
}