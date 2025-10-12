using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Выполняет цель "Атаковать игрока".
/// Система управляет состоянием атаки NPC:
/// 1. Если цель в радиусе и в конусе атаки, добавляет тег IsAttackingTag.
/// 2. Если цель вышла из зоны досягаемости, убирает IsAttackingTag.
/// 3. В состоянии атаки создает запросы на урон (PerformNpcAttackRequest) по кулдауну.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(NPCTaskCleanupSystem))]
public partial class AttackPlayerGoalExecutionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(World.Unmanaged);
        
        float time = (float)SystemAPI.Time.ElapsedTime;
        var em = EntityManager; 

        Entities
            .WithAll<HostileNPCTag, NPCBrain>()
            .WithNone<IsDeadTag>() 
            .ForEach((Entity e,
                      ref NPCMovementComponent movement,
                      ref AttackState attackState, 
                      in NPCBaseMovementStats baseMove,
                      in EnemyStats stats,
                      in ActiveGoal goal,
                      in LocalToWorld ltw) =>
            {
                // Проверяем валидность цели
                if (goal.Target == Entity.Null || !em.HasComponent<LocalToWorld>(goal.Target))
                {
                    // Если цель невалидна, нужно убедиться, что мы не застряли в состоянии атаки
                    if (em.HasComponent<IsAttackingTag>(e))
                    {
                        ecb.RemoveComponent<IsAttackingTag>(e);
                    }
                    return;
                }

                var tgtLTW = em.GetComponentData<LocalToWorld>(goal.Target);

                float distSq  = math.distancesq(ltw.Position, tgtLTW.Position);
                float atkR    = stats.AttackRange;
                bool  inRange = distSq <= atkR * atkR;

                // Проверяем, находится ли NPC уже в состоянии атаки
                bool isCurrentlyAttacking = em.HasComponent<IsAttackingTag>(e);

                
                bool canAttack = false; // Флаг, который определит, можем ли мы атаковать в этом кадре
                
                if (inRange)
                {
                    // Цель в радиусе. Теперь проверяем направление.
                    float3 npcForward = math.forward(ltw.Rotation);
                    float3 directionToPlayer = math.normalize(tgtLTW.Position - ltw.Position);
                    float dotProduct = math.dot(npcForward, directionToPlayer);

                    // Порог 0.5f соответствует углу обзора ~120 градусов.
                    const float attackAngleThreshold = 0.5f;

                    if (dotProduct > attackAngleThreshold)
                    {
                        // Цель и в радиусе, и в конусе атаки.
                        canAttack = true;
                    }
                }
                

                if (canAttack)
                {
                    // Переключаемся в состояние атаки, если еще не в нем.
                    if (!isCurrentlyAttacking)
                    {
                        ecb.AddComponent<IsAttackingTag>(e);
                    }
                    
                    // Устанавливаем желаемую дистанцию остановки, чтобы NPC не толкал игрока.
                    float desiredStop = math.max(baseMove.StoppingDistance, atkR * 0.9f);
                    movement.StoppingDistance = desiredStop;

                    // Атака по кулдауну
                    if (time >= attackState.LastAttackTime + stats.AttackCooldown)
                    {
                        var req = ecb.CreateEntity();
                        ecb.AddComponent(req, new PerformNpcAttackRequest
                        {
                            Attacker = e,
                            Target   = goal.Target,
                            Damage   = stats.Damage
                        });
                        attackState.LastAttackTime = time;
                    }
                }
                else
                {
                    // Цель не в зоне досягаемости (слишком далеко или не по направлению)
                    // Выключаем состояние атаки, чтобы NPC мог двигаться/поворачиваться.
                    if (isCurrentlyAttacking)
                    {
                        ecb.RemoveComponent<IsAttackingTag>(e);
                    }
                    
                    // Возвращаем стандартную дистанцию остановки.
                    movement.StoppingDistance = baseMove.StoppingDistance;
                }
            })
            .WithoutBurst().Run();
    }
}