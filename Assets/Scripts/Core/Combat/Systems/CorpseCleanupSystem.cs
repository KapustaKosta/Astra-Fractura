using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Финальная версия. Находит корень иерархии умирающего NPC и уничтожает все,
/// начиная с него, чтобы предотвратить появление любых сущностей-сирот.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
public partial class CorpseCleanupSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);

        // Этот блок ищет корневые сущности, которые были помечены для уничтожения в прошлом кадре.
        // Он запускает рекурсивное удаление всей иерархии, начиная с этого корня.
        Entities
            .WithoutBurst()
            .WithAll<IsDeadTag, Disabled>() // Ищем сущности, помеченные как мертвые и готовые к удалению.
            .ForEach((Entity rootEntity) =>
            {
                // Используем рекурсивную функцию для гарантированного уничтожения всех дочерних сущностей.
                DestroyHierarchy(rootEntity, ecb);

                // Дополнительно уничтожаем связанный GameObject, если он есть у корневой сущности.
                // Это необходимо для корректной очистки префабов, которые не являются чисто ECS-ными.
                if (EntityManager.HasComponent<GameObjectLink>(rootEntity))
                {
                    var goLink = EntityManager.GetComponentObject<GameObjectLink>(rootEntity);
                    if (goLink?.Value != null)
                    {
                        Object.Destroy(goLink.Value);
                    }
                }
            }).Run();

        // Этот блок находит сущности, которые только что умерли.
        Entities
            .WithAll<IsDeadTag>()
            .WithNone<Disabled>()
            .ForEach((Entity entity) =>
            {
                ecb.AddComponent<Disabled>(entity);

            }).Run();
        
        // После потенциального удаления NPC обновляем счетчик населения в поселении.
        UpdatePopulation();
    }

    /// <summary>
    /// Рекурсивно ставит в очередь на уничтожение сущность и всех ее потомков.
    /// </summary>
    private void DestroyHierarchy(Entity entity, EntityCommandBuffer ecb)
    {
        // Сначала рекурсивно вызываем для всех детей.
        if (SystemAPI.HasBuffer<Child>(entity))
        {
            foreach (var child in SystemAPI.GetBuffer<Child>(entity))
            {
                DestroyHierarchy(child.Value, ecb);
            }
        }
        // После того как все дети поставлены в очередь на уничтожение, уничтожаем саму сущность.
        ecb.DestroyEntity(entity);
    }

    /// <summary>
    /// Проверяет список NPC в поселении и удаляет из него несуществующие сущности.
    /// </summary>
    private void UpdatePopulation()
    {
        if (SystemAPI.TryGetSingletonEntity<PlayerSettlementTag>(out var settlementEntity))
        {
            var settlementRW = SystemAPI.GetComponentRW<SettlementComponent>(settlementEntity);
            var currentNpcs = settlementRW.ValueRW.NPCs;
            bool listChanged = false;
            
            // Итерируем с конца, чтобы безопасно удалять элементы из списка во время перебора.
            for (int i = currentNpcs.Length - 1; i >= 0; i--)
            {
                var npcEntity = currentNpcs[i];
                if (!EntityManager.Exists(npcEntity))
                {
                    currentNpcs.RemoveAt(i);
                    listChanged = true;
                }
            }

            // Обновляем счетчик населения только если список реально изменился.
            if (listChanged)
            {
                settlementRW.ValueRW.Population = currentNpcs.Length;
            }
        }
    }
}