using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics; 
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(PlayerAttackIntentionSystem))]
public partial class DamageSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var healthLookup = GetComponentLookup<HealthComponent>(isReadOnly: false);
        healthLookup.Update(this);

        var ecb = SystemAPI
            .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

        var itemRegistry = ItemRegistry.Instance;
        if (itemRegistry == null) return;


        if (!SystemAPI.TryGetSingleton<ImpactSystemConfig>(out var impactConfig))
        {
            // Если конфига нет, просто выходим, чтобы не применять отталкивание.
            impactConfig = default; 
        }

        float now = (float)SystemAPI.Time.ElapsedTime;

        Entities
            .WithoutBurst()
            .ForEach((Entity req, in PerformAttackRequest request) =>
            {
                // Цель должна быть валидной и живой на момент обработки
                if (!healthLookup.HasComponent(request.Target) ||
                    SystemAPI.HasComponent<IsDeadTag>(request.Target))
                {
                    ecb.DestroyEntity(req);
                    return;
                }

                // Должно быть оружие
                if (!SystemAPI.HasComponent<ActiveEquippedItem>(request.Attacker))
                {
                    ecb.DestroyEntity(req);
                    return;
                }

                var attackerItem = SystemAPI.GetComponent<ActiveEquippedItem>(request.Attacker);
                var itemData = itemRegistry.GetItemData(attackerItem.ItemID);
                if (itemData == null || itemData.itemType != ItemType.Weapon)
                {
                    ecb.DestroyEntity(req);
                    return;
                }

                // Наносим урон
                var hc = healthLookup[request.Target];
                hc.CurrentHealth -= itemData.weaponDamage;
                healthLookup[request.Target] = hc;

                // Обновляем LastHitInfo (направление/точка удара)
                float3 targetPos = SystemAPI.HasComponent<LocalToWorld>(request.Target)
                    ? SystemAPI.GetComponent<LocalToWorld>(request.Target).Position
                    : float3.zero;

                float3 hitDir = math.normalizesafe(targetPos - request.AttackerPosition);
                var hitInfo = new LastHitInfo {
                    AttackerPosition = request.AttackerPosition,
                    HitPoint         = targetPos + new float3(0, 0.8f, 0),
                    HitDirection     = hitDir,
                    Damage           = itemData.weaponDamage,
                    Time             = now
                };

                if (SystemAPI.HasComponent<LastHitInfo>(request.Target))
                    ecb.SetComponent(request.Target, hitInfo);
                else
                    ecb.AddComponent(request.Target, hitInfo);
                
                if (impactConfig.PlayerAttackKnockback > 0 && SystemAPI.HasComponent<PhysicsVelocity>(request.Target))
                {
                    // Направление "от атакующего"
                    float3 knockbackDirection = hitDir;
                    knockbackDirection.y = 0; // Для основного импульса используем только горизонтальное направление
                    
                    // Собираем итоговый импульс: назад + вверх
                    float3 impulse = (math.normalizesafe(knockbackDirection) * impactConfig.PlayerAttackKnockback) 
                                     + new float3(0, impactConfig.KnockbackUpwardForce, 0);

                    // Применяем импульс к цели
                    var velocity = SystemAPI.GetComponent<PhysicsVelocity>(request.Target);
                    velocity.Linear += impulse;
                    ecb.SetComponent(request.Target, velocity);
                }



                // Помечаем "в бою" только если цель ещё жива
                if (hc.CurrentHealth > 0f && SystemAPI.HasComponent<NPCComponent>(request.Target))
                {
                    var cs = new InCombat { LastDamageTime = now };
                    if (SystemAPI.HasComponent<InCombat>(request.Target))
                        ecb.SetComponent(request.Target, cs);
                    else
                        ecb.AddComponent(request.Target, cs);
                }

                // Запрос одноразовый
                ecb.DestroyEntity(req);
            })
            .Run();
    }
}