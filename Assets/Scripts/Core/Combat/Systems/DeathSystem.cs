using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using UnityEngine;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct DeathSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<HealthComponent>();
        state.RequireForUpdate<CombatSystemConfig>(); // Зависимость от конфига
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        
        // Получаем синглтон с настройками
        var config = SystemAPI.GetSingleton<CombatSystemConfig>();

        foreach (var (health, lastHit, entity) in
                 SystemAPI.Query<RefRO<HealthComponent>, RefRO<LastHitInfo>>()
                          .WithNone<IsDeadTag>()
                          .WithEntityAccess())
        {
            if (health.ValueRO.CurrentHealth > 0) continue;

            ecb.AddComponent<IsDeadTag>(entity);
            Debug.Log($"[DeathSystem] Entity {entity.Index} died: disable AI, enable ragdoll.");

            // 1) Отключаем поведенческие/движенческие компоненты
            if (SystemAPI.HasComponent<NPCBrain>(entity)) ecb.RemoveComponent<NPCBrain>(entity);
            if (SystemAPI.HasComponent<ActiveGoal>(entity)) ecb.RemoveComponent<ActiveGoal>(entity);
            if (SystemAPI.HasComponent<AIActiveTarget>(entity)) ecb.RemoveComponent<AIActiveTarget>(entity);
            if (SystemAPI.HasComponent<NPCMovementComponent>(entity)) ecb.RemoveComponent<NPCMovementComponent>(entity);
            if (SystemAPI.HasComponent<NPCPathfindingComponent>(entity)) ecb.RemoveComponent<NPCPathfindingComponent>(entity);
            if (SystemAPI.HasComponent<EnemySeenPlayer>(entity)) ecb.RemoveComponent<EnemySeenPlayer>(entity);
            if (SystemAPI.HasComponent<AttackState>(entity)) ecb.RemoveComponent<AttackState>(entity);
            if (SystemAPI.HasComponent<InCombat>(entity)) ecb.RemoveComponent<InCombat>(entity);

            // 2) ФИЗИКА: гарантируем ДИНАМИКУ + гравитацию + адекватное демпфирование
            // Снять временную кинематику, если применялась
            if (SystemAPI.HasComponent<PhysicsMassOverride>(entity))
            {
                var o = SystemAPI.GetComponent<PhysicsMassOverride>(entity);
                o.IsKinematic = 0;
                o.SetVelocityToZero = 0; // не стопорим скорость в кадр смерти
                ecb.SetComponent(entity, o);
            }

            // Проставить динамическую массу из коллайдера (иначе тело может быть "бесконечной массой")
            if (SystemAPI.HasComponent<PhysicsCollider>(entity))
            {
                var collider = SystemAPI.GetComponent<PhysicsCollider>(entity);
                if (collider.IsValid)
                {
                    var mp = collider.Value.Value.MassProperties;
                    var pm = Unity.Physics.PhysicsMass.CreateDynamic(mp, 70f); // подберите массу под тип NPC
                    ecb.SetComponent(entity, pm);
                }
            }

            // Гравитация включена
            if (SystemAPI.HasComponent<PhysicsGravityFactor>(entity))
            {
                ecb.SetComponent(entity, new PhysicsGravityFactor { Value = 1f });
            }

            // Усиленный демпф для имитации ослабления от отключённых систем (чтобы не «фиджет-спиннер»)
            if (SystemAPI.HasComponent<PhysicsDamping>(entity))
            {
                ecb.SetComponent(entity, new PhysicsDamping
                {
                    Linear  = 0.5f,  // Увеличено для быстрого затухания линейной скорости
                    Angular = 0.4f
                });
            }

            // 3) Ослабление и настройка скорости для естественного падения (линейная скорость от кнокбека сохраняется, но масштабируется)
            float3 awayDir = -lastHit.ValueRO.HitDirection;
            SetupVelocityOnDeath(ref state, ecb, entity, config, awayDir);

            // 4) Таймер до «момента диспауна/дропа»
            var timer = new DeathTimer
            {
                TimeSinceDeath   = 0f,
                TraceDuration    = config.CorpseLifetime, // Используем значение из конфига
                DropDone         = 0,
                DespawnRequested = 0
            };
            ecb.AddComponent(entity, timer);

            // Опционально — трейс/лог
            float3 hitXZ = lastHit.ValueRO.HitDirection; hitXZ.y = 0;
            var trace = new DeathTrace
            {
                Elapsed   = 0f,
                Duration  = config.CorpseLifetime, // Используем значение из конфига
                HitDirXZ  = math.normalizesafe(hitXZ)
            };
            ecb.AddComponent(entity, trace);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    // Вспомогательная: ослабление линейной скорости + угловой импульс (линейная скорость не трогаем, кроме масштабирования)
    private void SetupVelocityOnDeath(ref SystemState state, EntityCommandBuffer ecb, Entity e, CombatSystemConfig config, float3 awayDir)
    {
        if (!SystemAPI.HasComponent<PhysicsVelocity>(e)) return;

        var rng = Unity.Mathematics.Random.CreateFromIndex((uint)e.Index);
        var vel = SystemAPI.GetComponent<PhysicsVelocity>(e);
        
        // Ослабляем линейную скорость для имитации влияния отключённых систем движения
        vel.Linear *= config.KnockbackDeathMultiplier;  // Например, 0.7f — настраивается в конфиге
        
        // Устанавливаем угловую скорость (сохраняем Linear после масштабирования)
        vel.Angular = math.normalize(rng.NextFloat3Direction()) * 2.0f;

        // Клампы от аномалий
        vel.Angular = math.clamp(vel.Angular, new float3(-5f), new float3(5f));
        ecb.SetComponent(e, vel);
    }
}