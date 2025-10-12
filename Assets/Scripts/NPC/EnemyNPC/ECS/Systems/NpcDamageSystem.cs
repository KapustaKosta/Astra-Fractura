using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class NpcDamageSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var healthLookup = GetComponentLookup<HealthComponent>(false);
        var ltwLookup = GetComponentLookup<LocalToWorld>(true);

        var ecb = SystemAPI
            .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);
            
        float now = (float)SystemAPI.Time.ElapsedTime;

        if (!SystemAPI.TryGetSingleton<ImpactSystemConfig>(out var impactConfig))
        {
            impactConfig = default;
        }

        Entities
            .WithoutBurst()
            .ForEach((Entity reqEntity, in PerformNpcAttackRequest req) =>
            {
                // защищаемся от мусорных запросов
                if (!healthLookup.HasComponent(req.Target) || SystemAPI.HasComponent<IsDeadTag>(req.Target))
                {
                    ecb.DestroyEntity(reqEntity);
                    return;
                }

                // наносим урон на запрос
                var hp = healthLookup[req.Target];
                var oldHp = hp.CurrentHealth;
                hp.CurrentHealth = math.max(0, oldHp - req.Damage);
                healthLookup[req.Target] = hp;
                
                bool isPlayer = SystemAPI.HasComponent<PlayerTag>(req.Target);
                if (isPlayer)
                {
                    if (impactConfig.NpcAttackKnockback > 0 && 
                        ltwLookup.HasComponent(req.Attacker) && 
                        ltwLookup.HasComponent(req.Target))
                    {
                        var attackerPos = ltwLookup[req.Attacker].Position;
                        var targetPos = ltwLookup[req.Target].Position;
                        float3 knockbackDirection = math.normalizesafe(targetPos - attackerPos);
                        knockbackDirection.y = 0;

                        float3 impulse = (knockbackDirection * impactConfig.NpcAttackKnockback) 
                                         + new float3(0, impactConfig.KnockbackUpwardForce, 0);

                        var knockbackData = new PlayerKnockback
                        {
                            Velocity = impulse,
                            Damping = 0.96f 
                        };
                        
                        if (SystemAPI.HasComponent<PlayerKnockback>(req.Target))
                        {
                            var existingKnockback = SystemAPI.GetComponent<PlayerKnockback>(req.Target);
                            knockbackData.Velocity += existingKnockback.Velocity;
                            ecb.SetComponent(req.Target, knockbackData);
                        }
                        else
                        {
                            ecb.AddComponent(req.Target, knockbackData);
                        }
                    }
                }
                else // Для всех остальных NPC используем старую логику через PhysicsVelocity
                {
                    if (impactConfig.NpcAttackKnockback > 0 && 
                        SystemAPI.HasComponent<PhysicsVelocity>(req.Target) &&
                        ltwLookup.HasComponent(req.Attacker) && 
                        ltwLookup.HasComponent(req.Target))
                    {
                        var attackerPos = ltwLookup[req.Attacker].Position;
                        var targetPos = ltwLookup[req.Target].Position;
                        float3 knockbackDirection = math.normalizesafe(targetPos - attackerPos);
                        knockbackDirection.y = 0;
                        float3 impulse = (knockbackDirection * impactConfig.NpcAttackKnockback) 
                                         + new float3(0, impactConfig.KnockbackUpwardForce, 0);
                        var velocity = SystemAPI.GetComponent<PhysicsVelocity>(req.Target);
                        velocity.Linear += impulse;
                        ecb.SetComponent(req.Target, velocity);
                    }
                }

                var inCombat = new InCombat { LastDamageTime = now };
                if (SystemAPI.HasComponent<InCombat>(req.Target)) ecb.SetComponent(req.Target, inCombat);
                else ecb.AddComponent(req.Target, inCombat);

                //Debug.Log($"[NpcDamage] {req.Target.Index} took {req.Damage} -> {oldHp}→{hp.CurrentHealth}");

                // уничтожаем запрос, чтобы не тикал каждый кадр
                ecb.DestroyEntity(reqEntity);
            })
            .Run();
    }
}