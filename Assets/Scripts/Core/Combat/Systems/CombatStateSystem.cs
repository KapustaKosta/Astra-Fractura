// Assets/Scripts/Core/Combat/Systems/CombatStateSystem.cs
using Unity.Entities;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(DamageSystem))]
[UpdateAfter(typeof(DeathSystem))]
public partial class CombatStateSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = SystemAPI
            .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

        float currentTime = (float)SystemAPI.Time.ElapsedTime;

        if (!SystemAPI.TryGetSingleton<CombatSystemConfig>(out var config))
            return;

        float combatTimeoutDuration = config.CombatTimeoutDuration;

        foreach (var (inCombat, entity) in SystemAPI.Query<RefRO<InCombat>>()
                     .WithAll<NPCComponent>()
                     .WithNone<IsDeadTag, Disabled>() // добавили Disabled
                     .WithEntityAccess())
        {
            if (currentTime > inCombat.ValueRO.LastDamageTime + combatTimeoutDuration)
                ecb.RemoveComponent<InCombat>(entity);
        }
    }
}