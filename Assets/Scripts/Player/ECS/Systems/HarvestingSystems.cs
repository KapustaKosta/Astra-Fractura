using Unity.Entities;
using UnityEngine;

/// <summary>
/// Система реагирует на готовое намерение WantsToHarvestTag и выполняет логику добычи.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(HarvestIntentionSystem))] // Работает после системы по намерениям
public partial class HarvestingSystem : SystemBase
{
    private float harvestInterval = 0.5f;

    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        float currentTime = (float)SystemAPI.Time.ElapsedTime;
        
        // Этот запрос уже был правильным
        foreach (var (intention, playerState, interactionTarget, entity) in 
                 SystemAPI.Query<RefRO<WantsToHarvestTag>, RefRW<PlayerStateData>, RefRO<InteractionTarget>>()
                     .WithEntityAccess())
        {
            // Проверяем персональный таймер
            if (currentTime < playerState.ValueRO.LastHarvestTime + harvestInterval)
            {
                continue;
            }

            var targetEntity = interactionTarget.ValueRO.Value;

            // Проверяем, что цель все еще является ресурсным узлом, на всякий случай
            if (!SystemAPI.HasComponent<ResourceNode>(targetEntity))
            {
                continue;
            }
            
            // Добавляем тег для UI, чтобы показать, что процесс идет.
            var resourceNode = SystemAPI.GetComponent<ResourceNode>(targetEntity);
            ecb.AddComponent(entity, new IsHarvestingTag { ResourceType = resourceNode.resourceType });

            // Обновляем персональный таймер, чтобы запустить кулдаун.
            playerState.ValueRW.LastHarvestTime = currentTime;
        }

        // Этот запрос тоже был правильным
        var query = SystemAPI.QueryBuilder().WithAll<IsHarvestingTag>().WithNone<WantsToHarvestTag>().Build();
        ecb.RemoveComponent<IsHarvestingTag>(query);
    }
}


/// <summary>
/// Система, которая обрабатывает запросы на добычу, находя предмет и напрямую добавляя его в MonoBehaviour-инвентарь.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
public partial class ProcessHarvestRequestSystem : SystemBase
{
    private ResourceItemMapping resourceItemMapping;

    protected override void OnUpdate()
    {
        if (resourceItemMapping == null)
        {
            resourceItemMapping = Resources.Load<ResourceItemMapping>("ResourceItemMapping"); 
            if (resourceItemMapping == null)
            {
                Debug.LogError("ProcessHarvestRequestSystem: ResourceItemMapping не найден в папке Resources!");
                this.Enabled = false;
                return;
            }
        }

        var inventory = Inventory.Instance;
        if (inventory == null) return;
        
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);

        // --- ИСПРАВЛЕНИЕ ЗДЕСЬ ---
        // Получаем ComponentLookup для безопасного доступа к компонентам других сущностей
        var resourceNodeLookup = SystemAPI.GetComponentLookup<ResourceNode>(true); // true = ReadOnly

        Entities
            .ForEach((Entity entity, in HarvestRequest request) =>
            {
                // Используем ComponentLookup для проверки и получения компонента
                if (!resourceNodeLookup.HasComponent(request.TargetResourceNode)) return;

                var resourceNode = resourceNodeLookup[request.TargetResourceNode];
                Item itemToGive = resourceItemMapping.GetItemByResourceType(resourceNode.resourceType);

                if (itemToGive != null)
                {
                    inventory.Add(itemToGive, resourceNode.speedOfCollection);
                }

                ecb.DestroyEntity(entity);

            }).WithoutBurst().Run();
    }
}