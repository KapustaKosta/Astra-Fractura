using Unity.Entities;
using UnityEngine;

/// <summary>
/// Абстрактный базовый класс для определения AI цели в виде ScriptableObject.
/// Позволяет декларативно описывать поведение, оценку и условия выполнения цели.
/// </summary>
public abstract class GoalDefinition : ScriptableObject
{
    [Header("Основная информация")]
    [Tooltip("Тип цели, соответствующий перечислению GoalType.")]
    public GoalType Type;

    [Tooltip("Базовая оценка привлекательности этой цели. Может изменяться в ScoreGoal.")]
    public float BaseScore;

    /// <summary>
    /// Определяет, может ли NPC в принципе рассматривать эту цель.
    /// </summary>
    /// <param name="entity">Сущность NPC.</param>
    /// <param name="context">Контекст с данными из мира ECS для принятия решения.</param>
    /// <returns>True, если цель может быть рассмотрена.</returns>
    public abstract bool CanBeConsidered(Entity entity, in GoalEvaluationContext context);

    /// <summary>
    /// Оценивает, насколько привлекательна эта цель в текущем контексте.
    /// </summary>
    /// <param name="entity">Сущность NPC.</param>
    /// <param name="context">Контекст с данными из мира ECS для принятия решения.</param>
    /// <returns>Оценка привлекательности цели. 0, если цель невыполнима.</returns>
    public abstract float ScoreGoal(Entity entity, in GoalEvaluationContext context);

    /// <summary>
    /// Создает и возвращает компонент ActiveGoal для назначения NPC.
    /// </summary>
    /// <param name="entity">Сущность NPC.</param>
    /// <param name="context">Контекст с данными из мира ECS для принятия решения.</param>
    /// <param name="score">Оценка, с которой была выбрана эта цель.</param>
    /// <returns>Сконфигурированный компонент ActiveGoal.</returns>
    public abstract ActiveGoal CreateGoal(Entity entity, in GoalEvaluationContext context, float score);
}