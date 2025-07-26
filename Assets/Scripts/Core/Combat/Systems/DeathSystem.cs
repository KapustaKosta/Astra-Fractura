using Unity.Entities;

/// <summary>
/// Проверяет сущности со здоровьем. Если здоровье <= 0, добавляет тег IsDeadTag,
/// создает уведомление о смерти для NPC и очищает UI, если этот NPC был целью.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(DamageSystem))]
public partial class DeathSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);

        // Ищем сущности-NPC, у которых есть здоровье, но которые еще не помечены как мертвые.
        foreach (var (health, npcData, entity)
                 in SystemAPI.Query<RefRO<HealthComponent>, RefRO<NPCComponent>>()
                     .WithNone<IsDeadTag>()
                     .WithEntityAccess())
        {
            // Основное условие: здоровье упало до нуля или ниже.
            if (health.ValueRO.CurrentHealth <= 0)
            {
                // Помечаем сущность тегом IsDeadTag. Это сигнал для других систем 
                ecb.AddComponent<IsDeadTag>(entity);

                // Создаем сущность-запрос на показ уведомления в UI.
                var notificationEntity = ecb.CreateEntity();
                string message = $"{npcData.ValueRO.Name.ToString()} убит!";
                ecb.AddComponent(notificationEntity, new UINotificationRequest
                {
                    Message = message
                });

                // Проверяем, был ли умирающий NPC текущей целью боевого UI.
                // Если да, то синглтон, отвечающий за отображение UI, нужно уничтожить, чтобы UI скрылся.
                if (SystemAPI.TryGetSingletonEntity<ActiveCombatTarget>(out var singletonEntity))
                {
                    if (SystemAPI.GetComponent<ActiveCombatTarget>(singletonEntity).TargetEntity == entity)
                    {
                        ecb.DestroyEntity(singletonEntity);
                    }
                }
            }
        }
    }
}