using Unity.Entities;
using UnityEngine;

/// <summary>
/// Система, которая обрабатывает запросы на добычу (<c>HarvestRequest</c>), 
/// создавая запрос на добавление предмета (<c>AddItemRequest</c>) в инвентарь игрока.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
public partial class ProcessHarvestRequestSystem : SystemBase
{
    private ResourceItemMapping resourceItemMapping;

    /// <summary>
    /// Вызывается каждый кадр для обработки запросов.
    /// </summary>
    protected override void OnUpdate()
    {
        // При первом запуске или после перезагрузки домена загружаем ассет с сопоставлением ресурсов.
        if (resourceItemMapping == null)
        {
            resourceItemMapping = Resources.Load<ResourceItemMapping>("ResourceItemMapping"); 
            if (resourceItemMapping == null)
            {
                // Это критическая ошибка, система не сможет работать.
                Debug.LogError("[ProcessHarvestRequestSystem] " +
                               "КРИТИЧЕСКАЯ ОШИБКА: Ассет 'ResourceItemMapping'" +
                               " не найден в папке Resources! Система отключена.");
                this.Enabled = false;
                return;
            }
        }

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        var resourceNodeLookup = SystemAPI.GetComponentLookup<ResourceNode>(true); // ReadOnly

        Entities
            .ForEach((Entity requestEntity, in HarvestRequest request) =>
            {
                // Проверяем, что целевой ресурс все еще существует и является ресурсным узлом.
                if (!resourceNodeLookup.HasComponent(request.TargetResourceNode))
                {
                    ecb.DestroyEntity(requestEntity);
                    return;
                }

                var resourceNode = resourceNodeLookup[request.TargetResourceNode];
                
                // Получаем ScriptableObject предмета, соответствующий типу добываемого ресурса.
                Item itemToGive = resourceItemMapping.GetItemByResourceType(resourceNode.resourceType);
                
                if (itemToGive != null)
                {
                    // Создаем запрос на добавление предмета в инвентарь владельца.
                    var addItemRequestEntity = ecb.CreateEntity();
                    ecb.AddComponent(addItemRequestEntity, new AddItemRequest
                    {
                        TargetInventoryOwner = request.Player,
                        ItemID = itemToGive.itemID,
                        Amount = resourceNode.speedOfCollection
                    });
                }
                else
                {
                     Debug.LogError($"[ProcessHarvestRequestSystem] Не удалось создать AddItemRequest" +
                                    $", так как предмет для типа ресурса '{resourceNode.resourceType}'" +
                                    $" не был найден в ResourceItemMapping!");
                }
                
                ecb.DestroyEntity(requestEntity);

            }).WithoutBurst().Run();
    }
}