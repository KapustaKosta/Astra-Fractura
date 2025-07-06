using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine; // Для Inventory.Instance и Debug.Log

/// <summary>
/// Финальная система, которая обрабатывает запросы PlaceBuildingRequest,
/// инстанциирует здание и удаляет предмет из инвентаря.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
public partial class FinalizeBuildingSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Пытаемся получить синглтон-инвентарь
        var inventory = Inventory.Instance;
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(World.Unmanaged);

        // Если инвентарь отсутствует — уничтожаем все запросы и выходим
        if (inventory == null)
        {
            Debug.LogWarning("FinalizeBuildingSystem: Inventory instance not found. Cannot finalize building.");
            var requestsNoInv = SystemAPI.QueryBuilder()
                .WithAll<PlaceBuildingRequest>()
                .Build();

            if (!requestsNoInv.IsEmpty)
            {
                // Используем тот же ECB
                ecb.DestroyEntity(requestsNoInv, EntityQueryCaptureMode.AtPlayback);
            }
            return;
        }

        // Если состояние строительства отсутствует — удаляем все запросы
        if (!SystemAPI.TryGetSingleton<BuildingState>(out var buildingState))
        {
            var requestsNoState = SystemAPI.QueryBuilder()
                .WithAll<PlaceBuildingRequest>()
                .Build();

            if (!requestsNoState.IsEmpty)
                ecb.DestroyEntity(requestsNoState, EntityQueryCaptureMode.AtPlayback);
        }

        // Обрабатываем каждый запрос на постройку
        foreach (var (requestRO, requestEntity) in SystemAPI
                     .Query<RefRO<PlaceBuildingRequest>>()
                     .WithEntityAccess())
        {
            var request = requestRO.ValueRO;

            // Валидация префаба
            if (request.BuildingPrefabToPlace == Entity.Null ||
                !SystemAPI.Exists(request.BuildingPrefabToPlace))
            {
                Debug.LogError($"FinalizeBuildingSystem: Невалидный BuildingPrefabToPlace в запросе для Entity {requestEntity}. ItemID: {request.ItemIDToConsume}");
                ecb.DestroyEntity(requestEntity);
                continue;
            }

            // Инстанцирование здания
            var newBuilding = ecb.Instantiate(request.BuildingPrefabToPlace);
            ecb.SetComponent(newBuilding,
                LocalTransform.FromPositionRotation(request.Position, request.Rotation));
            ecb.AddComponent<NewlyBuiltTag>(newBuilding);

            // Удаление предмета из инвентаря
            var itemInInventory = inventory.items
                .Find(invItem => invItem.item.itemID == request.ItemIDToConsume);
            if (itemInInventory != null)
            {
                inventory.Remove(itemInInventory.item, 1);
            }
            else
            {
                Debug.LogWarning($"FinalizeBuildingSystem: Предмет с ID {request.ItemIDToConsume} не найден в инвентаре для удаления после постройки.");
            }

            // Уничтожаем обработанный запрос
            ecb.DestroyEntity(requestEntity);
        }
    }
}
