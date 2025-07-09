using Unity.Entities;
using UnityEngine;

/// <summary>
/// Система, которая обрабатывает запросы на добычу, создавая запрос на добавление предмета в инвентарь игрока.
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
                // Это критическая ошибка, система не сможет работать.
                Debug.LogError("[ProcessHarvestRequestSystem] КРИТИЧЕСКАЯ ОШИБКА: Ассет ResourceItemMapping не найден в папке Resources! Система отключена.");
                this.Enabled = false;
                return;
            }
        }

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        
        // Используем ComponentLookup для безопасного доступа к компонентам других сущностей
        var resourceNodeLookup = SystemAPI.GetComponentLookup<ResourceNode>(true); // true = ReadOnly

        Entities
            .ForEach((Entity requestEntity, in HarvestRequest request) =>
            {
                // --- DEBUG ---
                Debug.Log($"<color=orange>[ProcessHarvestRequestSystem]</color> Обнаружен HarvestRequest от игрока {request.Player} на цель {request.TargetResourceNode}.");
                // --- END DEBUG ---

                // Используем ComponentLookup для проверки и получения компонента
                if (!resourceNodeLookup.HasComponent(request.TargetResourceNode))
                {
                    Debug.LogWarning($"[ProcessHarvestRequestSystem] Цель {request.TargetResourceNode} в запросе больше не имеет компонента ResourceNode. Запрос удален.");
                    ecb.DestroyEntity(requestEntity);
                    return;
                }

                var resourceNode = resourceNodeLookup[request.TargetResourceNode];
                
                // --- DEBUG ---
                Debug.Log($"[ProcessHarvestRequestSystem] Тип ресурса у цели: {resourceNode.resourceType}. Скорость добычи: {resourceNode.speedOfCollection}.");
                // --- END DEBUG ---

                Item itemToGive = resourceItemMapping.GetItemByResourceType(resourceNode.resourceType);

                // --- DEBUG ---
                // Это самый важный лог. Если здесь "NULL", значит, проблема в вашем ResourceItemMapping.
                Debug.Log($"[ProcessHarvestRequestSystem] Попытка найти предмет для типа {resourceNode.resourceType}... Результат: {(itemToGive != null ? itemToGive.itemName : "!!! ПРЕДМЕТ НЕ НАЙДЕН (NULL) !!!")}");
                // --- END DEBUG ---

                if (itemToGive != null)
                {
                    // Создаем запрос на добавление предмета в инвентарь
                    var addItemRequestEntity = ecb.CreateEntity();
                    ecb.AddComponent(addItemRequestEntity, new AddItemRequest
                    {
                        TargetInventoryOwner = request.Player,
                        ItemID = itemToGive.itemID,
                        Amount = resourceNode.speedOfCollection
                    });
                     // --- DEBUG ---
                    Debug.Log($"<color=green>[ProcessHarvestRequestSystem]</color> Создаю AddItemRequest: Предмет '{itemToGive.itemName}' (ID: {itemToGive.itemID}), Количество: {resourceNode.speedOfCollection}");
                    // --- END DEBUG ---
                }
                else
                {
                     Debug.LogError($"[ProcessHarvestRequestSystem] Не удалось создать AddItemRequest, так как предмет для типа {resourceNode.resourceType} не был найден в ResourceItemMapping!");
                }

                // Уничтожаем обработанный запрос на добычу
                ecb.DestroyEntity(requestEntity);

            }).WithoutBurst().Run();
    }
}