using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine; // Для Inventory.Instance и Debug.Log
using Unity.Collections; // Для NativeList, если вдруг понадобится (хотя здесь не напрямую)

/// <summary>
/// Финальная система, которая обрабатывает запросы PlaceBuildingRequest,
/// инстанциирует здание и удаляет предмет из инвентаря.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
public partial class FinalizeBuildingSystem : SystemBase
{
    /// <summary>
    /// Вызывается каждый кадр для обработки запросов на постройку.
    /// Система инстанциирует здание в мире, используя данные из PlaceBuildingRequest,
    /// и затем пытается удалить соответствующий предмет из инвентаря игрока.
    /// Обработанные запросы уничтожаются.
    /// </summary>
    protected override void OnUpdate()
    {
        var inventory = Inventory.Instance;
        // Проверяем наличие экземпляра инвентаря. Если его нет, логируем предупреждение
        // и уничтожаем все текущие запросы на постройку, чтобы они не зависали.
        if (inventory == null)
        {
            Debug.LogWarning("FinalizeBuildingSystem: Inventory instance not found. Cannot finalize building.");
            var requestsQueryNoInventory = SystemAPI.QueryBuilder().WithAll<PlaceBuildingRequest>().Build();
            if (!requestsQueryNoInventory.IsEmpty)
            {
                var ecbOnNoInventory = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(World.Unmanaged);
                ecbOnNoInventory.DestroyEntity(requestsQueryNoInventory);
            }
            return;
        }
        
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        
        // Проверяем наличие BuildingState. Если его нет (например, если система сработала после выхода из режима строительства),
        // уничтожаем все запросы на постройку.
        if (!SystemAPI.TryGetSingleton<BuildingState>(out var buildingState))
        {
            var requestsQueryNoBuildingState = SystemAPI.QueryBuilder().WithAll<PlaceBuildingRequest>().Build();
            if (!requestsQueryNoBuildingState.IsEmpty)
            {
                ecb.DestroyEntity(requestsQueryNoBuildingState);
            }
            return;
        }

        // Итерируем по всем сущностям с компонентом PlaceBuildingRequest.
        foreach (var (requestRO, requestEntity) in SystemAPI.Query<RefRO<PlaceBuildingRequest>>().WithEntityAccess())
        {
            var request = requestRO.ValueRO; 

            // Проверяем валидность префаба здания, указанного в запросе.
            if (request.BuildingPrefabToPlace == Entity.Null || !SystemAPI.Exists(request.BuildingPrefabToPlace))
            {
                Debug.LogError($"FinalizeBuildingSystem: Невалидный BuildingPrefabToPlace в запросе на постройку для Entity:" +
                               $" {requestEntity}. ItemID: {request.ItemIDToConsume}");
                ecb.DestroyEntity(requestEntity); // Уничтожаем невалидный запрос.
                continue; // Переходим к следующему запросу.
            }

            // Инстанциируем новое здание из префаба и устанавливаем его позицию и вращение.
            var newBuilding = ecb.Instantiate(request.BuildingPrefabToPlace);
            ecb.SetComponent(newBuilding, LocalTransform.FromPositionRotation(request.Position, request.Rotation));
            ecb.AddComponent<NewlyBuiltTag>(newBuilding); // Добавляем тег.
            
            // Пытаемся удалить предмет из инвентаря, который был использован для постройки.
            var itemInInventory = inventory.items.Find(invItem => invItem.item.itemID == request.ItemIDToConsume);
            if (itemInInventory != null)
            {
                inventory.Remove(itemInInventory.item, 1);
            }
            else
            {
                Debug.LogWarning($"FinalizeBuildingSystem: Предмет с ID {request.ItemIDToConsume}" +
                                 $" не найден в инвентаре для удаления после постройки.");
            }
            
            // Уничтожаем обработанный запрос на постройку.
            ecb.DestroyEntity(requestEntity);
        }
    }
}