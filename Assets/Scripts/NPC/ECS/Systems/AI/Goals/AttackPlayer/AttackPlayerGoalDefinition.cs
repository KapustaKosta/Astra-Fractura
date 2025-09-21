using Unity.Entities;
using UnityEngine;

/// <summary>
/// Цель "Атаковать игрока".
/// </summary>
[CreateAssetMenu(fileName = "Goal_AttackPlayer", menuName = "AI/Goal Definitions/Attack Player")]
[Cleanup(typeof(AIActiveTarget))]
public class AttackPlayerGoalDefinition : GoalDefinition
{
    [Tooltip("Базовый приоритет для врагов")]
    public float BaseScore = 1000f;

    public override bool CanBeConsidered(Entity entity, in GoalEvaluationContext context)
    {
        var em = context.EntityManager;

        if (!em.HasComponent<EnemySeenPlayer>(entity))
        {
            //Debug.Log($"[AttackGoal.CanBeConsidered] {entity.Index}: FAIL no EnemySeenPlayer");
            return false;
        }

        var esp = em.GetComponentData<EnemySeenPlayer>(entity);
        var player = esp.Player;

        if (player == Entity.Null)
        {
            //Debug.Log($"[AttackGoal.CanBeConsidered] {entity.Index}: FAIL EnemySeenPlayer.Player == Null");
            return false;
        }

        bool hasPlayerLTW    = em.HasComponent<Unity.Transforms.LocalToWorld>(player);
        bool hasPlayerHealth = em.HasComponent<HealthComponent>(player);
        bool isPlayerDead    = em.HasComponent<IsDeadTag>(player);
        bool movementFailed  = em.HasComponent<MovementFailedTag>(entity);

        if (!hasPlayerLTW)
        {
            //Debug.Log($"[AttackGoal.CanBeConsidered] {entity.Index}: FAIL player({player.Index}) no LocalToWorld");
            return false;
        }
        if (!hasPlayerHealth)
        {
            //Debug.Log($"[AttackGoal.CanBeConsidered] {entity.Index}: FAIL player({player.Index}) no HealthComponent");
            return false;
        }
        if (isPlayerDead)
        {
            //Debug.Log($"[AttackGoal.CanBeConsidered] {entity.Index}: FAIL player({player.Index}) IsDeadTag");
            return false;
        }
        if (movementFailed)
        {
            //Debug.Log($"[AttackGoal.CanBeConsidered] {entity.Index}: FAIL MovementFailedTag on enemy");
            return false;
        }

        //Debug.Log($"[AttackGoal.CanBeConsidered] {entity.Index}: OK -> target player({player.Index})");
        return true;
    }

    public override float ScoreGoal(Entity entity, in GoalEvaluationContext context)
    {
        return BaseScore;
    }

    public override ActiveGoal CreateGoal(Entity entity, in GoalEvaluationContext context, float score)
    {
        var esp = context.EntityManager.GetComponentData<EnemySeenPlayer>(entity);
        var goal = new ActiveGoal { Type = GoalType.Attack, Target = esp.Player, CurrentGoalScore = score };
        //Debug.Log($"[AttackGoal.CreateGoal] {entity.Index}: Attack -> target player({esp.Player.Index}) score={score:F1}");
        return goal;
    }
}
