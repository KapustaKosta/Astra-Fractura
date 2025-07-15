using Unity.Entities;
using UnityEngine;

/// <summary>
/// Определение цели "Бездействие" для ИИ.
/// Эта цель всегда доступна и используется как резервная/нейтральная задача.
/// Создается через меню Unity (AI/Goal Definitions/Idle).
/// </summary>
[CreateAssetMenu(fileName = "Goal_Idle", menuName = "AI/Goal Definitions/Idle")]
public class IdleGoalDefinition : GoalDefinition
{
    /// <summary>
    /// Проверяет, может ли сущность рассматривать эту цель.
    /// Цель "Бездействие" всегда доступна для любого NPC.
    /// </summary>
    public override bool CanBeConsidered(Entity entity, in GoalEvaluationContext context) => true;

    /// <summary>
    /// Возвращает базовый балл для цели без дополнительных факторов.
    /// Используется как минимальный приоритет в системе выбора целей.
    /// </summary>
    public override float ScoreGoal(Entity entity, in GoalEvaluationContext context) => BaseScore;

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