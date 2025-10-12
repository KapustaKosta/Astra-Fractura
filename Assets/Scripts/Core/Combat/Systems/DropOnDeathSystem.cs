using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class DropOnDeathSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = SystemAPI
            .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

        float now = (float)SystemAPI.Time.ElapsedTime;

        // Получаем синглтон с настройками
        if (!SystemAPI.TryGetSingleton<CombatSystemConfig>(out var config))
            return;

        // Обрабатываем ТОЛЬКО "готовых к дропу" мертвецов.
        Entities
            .WithoutBurst()
            .WithAll<IsDeadTag, DropRequested>()
            .ForEach((Entity deadNpc,
                      ref DynamicBuffer<InventoryItemElement> inv,
                      in LocalToWorld ltw) =>
            {
                float3 center = ltw.Position;

                // 1) Создаем тикет для отложенного спавна
                var ticket = ecb.CreateEntity();
                ecb.AddComponent(ticket, new LootSpawnTicket
                {
                    Position    = center + new float3(0, 0.5f, 0),
                    DelayFrames = config.DropDelayFrames, // Используем значение из конфига
                    SeedBase    = (uint)(deadNpc.Index ^ (int)(now * 1000f))
                });

                var buf = ecb.AddBuffer<LootItemElement>(ticket);

                // 2) Переносим весь инвентарь в тикет
                for (int i = 0; i < inv.Length; i++)
                {
                    var it = inv[i];
                    if (it.Amount <= 0 || it.ItemID <= 0) continue;
                    buf.Add(new LootItemElement { ItemID = it.ItemID, Amount = it.Amount });
                }

                // 3) Чистим инвентарь трупа
                inv.Clear();

                // 4) Снимаем запрос дропа (обязательно!)
                if (SystemAPI.HasComponent<DropRequested>(deadNpc))
                    ecb.RemoveComponent<DropRequested>(deadNpc);

                // 5) В ЭТОТ ЖЕ КАДР переводим сущность в Disabled и помечаем к очистке
                if (!SystemAPI.HasComponent<Disabled>(deadNpc))
                    ecb.AddComponent<Disabled>(deadNpc);

                if (!SystemAPI.HasComponent<DeadCleanupReady>(deadNpc))
                    ecb.AddComponent<DeadCleanupReady>(deadNpc);

                // Также отметим в таймере, что мы запросили диспаунинг
                if (SystemAPI.HasComponent<DeathTimer>(deadNpc))
                {
                    var t = SystemAPI.GetComponent<DeathTimer>(deadNpc);
                    t.DespawnRequested = 1;
                    ecb.SetComponent(deadNpc, t);
                }
            })
            .Run();
    }
}