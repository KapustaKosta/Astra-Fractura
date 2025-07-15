using Unity.Entities;
using UnityEngine;

/// <summary>
/// Система, которая обрабатывает запросы ValidateHarvestAttemptRequest.
/// Она проверяет, можно ли добыть ресурс, и если да, создает запрос на добавление
/// соответствующего предмета в инвентарь добытчика.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(HarvestingSystem))]
public partial class HarvestValidationSystem : SystemBase
{
    private ResourceItemMapping resourceItemMapping;
    
    protected override void OnCreate()
    {
        // Система зависит от ScriptableObject, поэтому мы готовимся к его загрузке.
    }

    protected override void OnUpdate()
    {
        // Используем ленивую загрузку, чтобы избежать проблем при перезагрузке доменов в редакторе Unity.
        if (resourceItemMapping == null)
        {
            resourceItemMapping = Resources.Load<ResourceItemMapping>("ResourceItemMapping");
            if (resourceItemMapping == null)
            {
                // Если критически важный ассет не найден, отключаем систему для предотвращения ошибок.
                #if UNITY_EDITOR
                Debug.LogError("Ассет 'ResourceItemMapping' не найден в папке Resources. Система валидации добычи будет отключена.");
                #endif
                this.Enabled = false;
                return;
            }
        }
        
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        var resourceNodeLookup = SystemAPI.GetComponentLookup<ResourceNode>(true);
        
        // Захватываем локальную ссылку для безопасного использования в лямбда-выражении.
        var localMapping = resourceItemMapping;

        Entities
            .WithReadOnly(resourceNodeLookup)
            // Система должна работать в основном потоке из-за доступа к управляемому объекту (ScriptableObject).
            .WithoutBurst() 
            .ForEach((Entity requestEntity, in ValidateHarvestAttemptRequest request) =>
            {
                // Убеждаемся, что целевой ресурс все еще существует.
                if (!resourceNodeLookup.HasComponent(request.TargetResourceNode))
                {
                    ecb.DestroyEntity(requestEntity);
                    return;
                }
                
                // В этом месте можно добавить дополнительные проверки, например,
                // есть ли у добытчика необходимый инструмент для добычи этого ресурса.
                bool canHarvest = true;

                if (canHarvest)
                {
                    var resourceNode = resourceNodeLookup[request.TargetResourceNode];
                    
                    // Используя карту ресурсов, определяем, какой предмет должен быть выдан.
                    Item itemToGive = localMapping.GetItemByResourceType(resourceNode.resourceType);

                    if (itemToGive != null)
                    {
                        // Если предмет определен, создаем новый запрос на добавление этого предмета в инвентарь.
                        var addItemRequestEntity = ecb.CreateEntity();
                        ecb.AddComponent(addItemRequestEntity, new AddItemRequest
                        {
                            TargetInventoryOwner = request.Harvester,
                            ItemID = itemToGive.itemID,
                            Amount = resourceNode.speedOfCollection
                        });
                    }
                }
                ecb.DestroyEntity(requestEntity);
            }).Run();
    }
}