using Unity.Entities;
using UnityEngine;

/// <summary>
/// Определение цели "Бездействие" для ИИ.
/// Эта цель используется как резервная для НАняТЫХ NPC, у которых нет других задач.
/// </summary>
[CreateAssetMenu(fileName = "Goal_Idle", menuName = "AI/Goal Definitions/Idle")]
public class IdleGoalDefinition : GoalDefinition
{
    /// <summary>
    /// Проверяет, может ли сущность рассматривать эту цель.
    /// Цель "Бездействие" доступна только для нанятых и не враждебных NPC.
    /// </summary>
    public override bool CanBeConsidered(Entity entity, in GoalEvaluationContext context)
    {
// Это не позволит врагам и не нанятым NPC выбирать эту цель.
        bool isHired = context.EntityManager.HasComponent<NPCHiredTag>(entity);
        bool isHostile = context.EntityManager.HasComponent<HostileNPCTag>(entity);
        return isHired && !isHostile;
    }

    /// <summary>
    /// Возвращает базовый балл для цели без дополнительных факторов.
    /// Этот балл должен быть самым низким среди всех "рабочих" целей.
    /// </summary>
    public override float ScoreGoal(Entity entity, in GoalEvaluationContext context)
    {
        return BaseScore;
    }

    /// <summary>
    /// Создает активную цель "Бездействие".
    /// Не требует целевого объекта и имеет фиксированный балл.
    /// </summary>
    public override ActiveGoal CreateGoal(Entity entity, in GoalEvaluationContext context, float score)
    {
        return new ActiveGoal
        {
            Type = this.Type,
            Target = Entity.Null, // Цель не привязана к объекту
            CurrentGoalScore = score
        };
    }
}