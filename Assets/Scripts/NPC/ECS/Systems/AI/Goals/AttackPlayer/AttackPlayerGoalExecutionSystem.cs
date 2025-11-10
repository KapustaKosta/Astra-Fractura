using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Выполняет цель "Атаковать игрока".
/// Система управляет состоянием атаки NPC:
/// 1. Если цель в радиусе атаки, добавляет тег IsAttackingTag, который сигнализирует
///    другим системам, что NPC должен остановиться.
/// 2. Если цель вышла из радиуса, убирает IsAttackingTag, позволяя NPC снова двигаться.
/// 3. Когда NPC находится в состоянии атаки (с тегом IsAttackingTag),
///    она создает запросы на атаку (PerformNpcAttackRequest) по кулдауну.
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
        var em = EntityManager; // Кэшируем EntityManager для проверок

        Entities
            .WithAll<HostileNPCTag, NPCBrain>()
            .ForEach((Entity e,
                      // ref NPCMovementComponent movement,
                      ref NPCMovementComponent movement,
                      ref NPCAnimationState animationState, 
                      // ref AttackState, т.к. мы будем менять LastAttackTime
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

                if (inRange)
                {
                    // Если мы еще не в состоянии атаки, переключаемся в него.
                    // Этот тег теперь является сигналом для AIPathfindingBridgeSystem, чтобы он
                    // прекратил назначать этому NPC путь.
                    if (!isCurrentlyAttacking)
                    {
                        ecb.AddComponent<IsAttackingTag>(e);
                    }

                    // Устанавливаем желаемую дистанцию остановки, чтобы NPC не толкал игрока.
                    // Он будет стараться держаться на 90% дистанции атаки.
                    float desiredStop = math.max(baseMove.StoppingDistance, atkR * 0.9f);
                    movement.StoppingDistance = desiredStop;

                    // Атака по кулдауну
                    if (time >= attackState.LastAttackTime + stats.AttackCooldown)
                    {
                        // Устанавливаем триггер анимации
                        animationState.AttackTrigger = true;
                        
                        // Создаем одноразовый запрос на нанесение урона
                        var req = ecb.CreateEntity();
                        ecb.AddComponent(req, new PerformNpcAttackRequest
                        {
                            Attacker = e,
                            Target   = goal.Target,
                            Damage   = stats.Damage
                        });

                        // Обновляем время последней атаки
                        attackState.LastAttackTime = time;
                    }
                }
                else
                {
                    // Если мы были в состоянии атаки, но цель вышла из радиуса,
                    // выключаем это состояние, чтобы NPC снова начал двигаться к цели.
                    if (isCurrentlyAttacking)
                    {
                        ecb.RemoveComponent<IsAttackingTag>(e);
                    }

                    // Возвращаем стандартную дистанцию остановки, чтобы NPC
                    // подходил к цели вплотную перед атакой.
                    movement.StoppingDistance = baseMove.StoppingDistance;
                }
            })
            .WithoutBurst().Run();
    }
}