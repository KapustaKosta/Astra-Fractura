using Unity.Entities;
using UnityEngine;

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

        // Используем ComponentLookup для безопасного доступа к компонентам других сущностей
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