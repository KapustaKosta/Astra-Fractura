using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(DropOnDeathSystem))] // сначала сформировать тикеты, потом — отрабатывать
public partial class LootSpawnSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = SystemAPI
            .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

        float now = (float)SystemAPI.Time.ElapsedTime;

        // Построим карту визуалов: ItemID -> Prefab
        var mapQuery = SystemAPI.QueryBuilder().WithAll<ItemVisualPrefabReference>().Build();
        var allRefs  = mapQuery.ToComponentDataArray<ItemVisualPrefabReference>(Allocator.Temp);
        var visualMap = new NativeHashMap<int, Entity>(math.max(1, allRefs.Length), Allocator.Temp);
        for (int i = 0; i < allRefs.Length; i++)
            visualMap.TryAdd(allRefs[i].ItemID, allRefs[i].EntityPrefab);
        allRefs.Dispose();

        // Проходим по тикетам
        Entities
            .WithoutBurst()
            .WithReadOnly(visualMap)
            .ForEach((Entity ticket,
                      ref LootSpawnTicket info,
                      ref DynamicBuffer<LootItemElement> items) =>
            {
                // Декремент кадровой задержки
                if (info.DelayFrames > 0)
                {
                    info.DelayFrames--;
                    return;
                }

                float3 dropPoint = info.Position;

                // Спавн лута
                for (int i = 0; i < items.Length; i++)
                {
                    var it = items[i];
                    if (it.Amount <= 0 || it.ItemID <= 0) continue;

                    visualMap.TryGetValue(it.ItemID, out var prefab);

                    // Логическая сущность предмета
                    var logical = ecb.CreateEntity();
                    ecb.AddComponent(logical, new WorldItem { ItemID = it.ItemID, Count = it.Amount });
                    ecb.AddComponent<DroppedItemTag>(logical);

                    if (prefab != Entity.Null)
                    {
                        var visual = ecb.Instantiate(prefab);
                        ecb.AddComponent(visual, new PendingDroppedVisualInit
                        {
                            Position = dropPoint,
                            Seed     = info.SeedBase ^ (uint)(i + 1),
                            Logical  = logical,
                            ItemID   = it.ItemID,
                            Amount   = it.Amount
                        });
                    }
                }

                // Тикет больше не нужен
                ecb.DestroyEntity(ticket);
            })
            .Run();

        visualMap.Dispose();
    }
}
