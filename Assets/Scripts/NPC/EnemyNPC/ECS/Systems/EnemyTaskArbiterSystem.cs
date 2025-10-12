using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Physics;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(HarvestConditionSystem))]
[UpdateAfter(typeof(EnemyPerceptionSystem))] 
public partial class EnemyTaskArbiterSystem : SystemBase
{
    private GoalRegistrySystem _goalRegistry;
    private EntityQuery _enemyQuery;

    private const float CURRENT_GOAL_INERTIA_BONUS = 1.1f;

    protected override void OnCreate()
    {
        base.OnCreate();
        _goalRegistry = World.GetOrCreateSystemManaged<GoalRegistrySystem>();
        RequireForUpdate<GoalRegistrySystem.Initialized>();
        
        _enemyQuery = GetEntityQuery(
            ComponentType.ReadOnly<HostileNPCTag>(),
            ComponentType.Exclude<IsDeadTag>() 
        );

    }


    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // сбрасываем невалидные текущие цели
        Entities
            .WithAll<ActiveGoal, CurrentGoalInvalidTag, HostileNPCTag>()
            .ForEach((Entity entity, in ActiveGoal goal) =>
            {
                ecb.AddComponent(entity, new CleanupGoalRequest { OldGoalType = goal.Type });
                ecb.RemoveComponent<ActiveGoal>(entity);
                ecb.RemoveComponent<CurrentGoalInvalidTag>(entity);
            }).Run();

        var map = _goalRegistry.GoalDefinitionsMap;
        if (map == null || map.Count == 0) { ecb.Dispose(); return; }
        if (!map.TryGetValue(GoalType.Attack, out var attackDef))
        {
            //Debug.LogWarning("[EnemyTaskArbiter] No Attack GoalDefinition in registry.");
            ecb.Dispose(); return;
        }

        if (!SystemAPI.TryGetSingleton<AISettings>(out var settings)) { ecb.Dispose(); return; }
        if (!SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var physics)) { ecb.Dispose(); return; }

        // минимальный контекст (без settlement/resource map)
        var context = new GoalEvaluationContext(
            EntityManager,
            settings,
            Entity.Null,
            physics.CollisionWorld,
            GetComponentLookup<ResourceNode>(true),
            GetComponentLookup<LocalToWorld>(true),
            GetBufferLookup<InventoryItemElement>(true),
            null
        );

        var enemies = _enemyQuery.ToEntityArray(Allocator.Temp);
        int considered = 0, chosen = 0, noSeen = 0;

        foreach (var e in enemies)
        {
            considered++;
            
            bool hasSeen = SystemAPI.HasComponent<EnemySeenPlayer>(e);
            if (!hasSeen)
            {
                noSeen++;
                //Debug.Log($"[EnemyTaskArbiter][Diag] {e.Index}: no EnemySeenPlayer yet");
                continue;
            }

            var esp = SystemAPI.GetComponent<EnemySeenPlayer>(e);
            var player = esp.Player;
            if (player == Entity.Null)
            {
                //Debug.Log($"[EnemyTaskArbiter][Diag] {e.Index}: EnemySeenPlayer.Player == Null");
                continue;
            }
            
            // Проверяем, жив ли игрок, которого мы "видим".
            if (SystemAPI.HasComponent<DeadTag>(player))
            {
                // Если игрок мертв, игнорируем его как цель.
                continue;
            }

            //Debug.Log($"[EnemyTaskArbiter][Diag] {e.Index}: seen=1, player={player.Index}, LTW={hasPlayerLTW}, HP={hasPlayerHealth}, dead={isPlayerDead}, moveFailed={movementFailed}");

            // проверяем CanBeConsidered (она теперь тоже логирует причины)
            if (!attackDef.CanBeConsidered(e, in context))
            {
                //Debug.Log($"[EnemyTaskArbiter] {e.Index}: Attack.CanBeConsidered == FALSE");
                continue;
            }

            bool hasActive = SystemAPI.HasComponent<ActiveGoal>(e);
            var current = hasActive ? SystemAPI.GetComponent<ActiveGoal>(e) : default;

            float score = attackDef.ScoreGoal(e, in context);
            if (hasActive && current.Type == GoalType.Attack)
                score *= CURRENT_GOAL_INERTIA_BONUS;

            var newGoal = attackDef.CreateGoal(e, in context, score);
            if (hasActive)
            {
                if (current.Type != newGoal.Type || current.Target != newGoal.Target)
                {
                    ecb.RemoveComponent<ActiveGoal>(e);
                    ecb.AddComponent(e, new CleanupGoalRequest { OldGoalType = current.Type });
                    ecb.AddComponent(e, newGoal);
                }
            }
            else
            {
                ecb.AddComponent(e, newGoal);
            }

            // для моста движения
            if (!SystemAPI.HasComponent<AIActiveTarget>(e))
                ecb.AddComponent(e, new AIActiveTarget { Value = newGoal.Target });
            else
                ecb.SetComponent(e, new AIActiveTarget { Value = newGoal.Target });

            //Debug.Log($"[EnemyTaskArbiter] {e.Index}:{e.Version} -> Attack target={(newGoal.Target==Entity.Null?"NULL":newGoal.Target.Index.ToString())}, score={score:F1}");
            chosen++;
        }

        //Debug.Log($"[EnemyTaskArbiter] hostiles={enemies.Length}, considered={considered}, chosen={chosen}, noSeenPlayer={noSeen}");

        enemies.Dispose();
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}