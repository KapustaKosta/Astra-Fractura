using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class NpcDamageSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var healthLookup = GetComponentLookup<HealthComponent>(false);
        var ecb = SystemAPI
            .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);
        float now = (float)SystemAPI.Time.ElapsedTime;

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

                // в бой для UI/индикаторов
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