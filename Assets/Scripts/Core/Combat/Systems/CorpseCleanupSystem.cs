// Assets/Scripts/Core/Combat/Systems/CorpseCleanupSystem.cs
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
public partial class CorpseCleanupSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities
            .WithStructuralChanges()
            .WithoutBurst()
            .WithAll<IsDeadTag, DeadCleanupReady, Disabled>()
            .ForEach((Entity rootEntity) =>
            {
                // 1. Сначала уничтожаем GameObject, пока сущность еще существует
                if (EntityManager.Exists(rootEntity) &&
                    EntityManager.HasComponent<GameObjectLink>(rootEntity))
                {
                    var goLink = EntityManager.GetComponentObject<GameObjectLink>(rootEntity);
                    if (goLink?.Value != null)
                        Object.Destroy(goLink.Value);

                    // Удаляем компонент-ссылку, чтобы избежать повторных попыток
                    if (EntityManager.Exists(rootEntity) &&
                        EntityManager.HasComponent<GameObjectLink>(rootEntity))
                    {
                        EntityManager.RemoveComponent<GameObjectLink>(rootEntity);
                    }
                }

                // 2. Теперь безопасно уничтожаем саму сущность и ее иерархию
                DestroyHierarchySafe(rootEntity);
            })
            .Run();

        UpdatePopulation();
    }

    private void DestroyHierarchySafe(Entity e)
    {
        if (!EntityManager.Exists(e)) return;

        if (EntityManager.HasBuffer<Child>(e))
        {
            // Копируем буфер ДО структурных изменений
            var children = EntityManager.GetBuffer<Child>(e).ToNativeArray(Allocator.Temp);
            for (int i = 0; i < children.Length; i++)
                DestroyHierarchySafe(children[i].Value);
            children.Dispose();
        }

        if (EntityManager.Exists(e))
            EntityManager.DestroyEntity(e);
    }

    private void UpdatePopulation()
    {
        if (SystemAPI.TryGetSingletonEntity<PlayerSettlementTag>(out var settlementEntity))
        {
            var settlementRW = SystemAPI.GetComponentRW<SettlementComponent>(settlementEntity);
            var currentNpcs  = settlementRW.ValueRW.NPCs;
            bool changed     = false;

            for (int i = currentNpcs.Length - 1; i >= 0; i--)
            {
                var npcEntity = currentNpcs[i];
                if (!EntityManager.Exists(npcEntity))
                {
                    currentNpcs.RemoveAt(i);
                    changed = true;
                }
            }

            if (changed)
                settlementRW.ValueRW.Population = currentNpcs.Length;
        }
    }
}
