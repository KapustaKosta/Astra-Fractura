using Unity.Entities;
using Unity.Transforms;

/// <summary>
/// Финальная система, которая обрабатывает запросы PlaceBuildingRequest,
/// инстанциирует здание и создает запрос на удаление предмета из инвентаря игрока.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
public partial class FinalizeBuildingSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Система больше не зависит от Inventory.Instance
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);

        // Получаем сущность игрока, чтобы знать, из чьего инвентаря удалять предмет.
        if (!SystemAPI.TryGetSingletonEntity<PlayerControllerData>(out var playerEntity))
        {
            // Если игрока нет, запросы на постройку не могут быть выполнены.
            var requestsQuery = SystemAPI.QueryBuilder().WithAll<PlaceBuildingRequest>().Build();
            ecb.DestroyEntity(requestsQuery, EntityQueryCaptureMode.AtPlayback);
            return;
        }

        // Обрабатываем каждый запрос на постройку
        foreach (var (request, requestEntity) in SystemAPI.Query<RefRO<PlaceBuildingRequest>>().WithEntityAccess())
        {
            var reqData = request.ValueRO;

            if (reqData.BuildingPrefabToPlace == Entity.Null || !SystemAPI.Exists(reqData.BuildingPrefabToPlace))
            {
                ecb.DestroyEntity(requestEntity);
                continue;
            }
            
            // Инстанцирование здания
            var newBuilding = ecb.Instantiate(reqData.BuildingPrefabToPlace);
            ecb.SetComponent(newBuilding, LocalTransform.FromPositionRotation(reqData.Position, reqData.Rotation));
            ecb.AddComponent<NewlyBuiltTag>(newBuilding);
            // Новый тег для гибридного спавна GameObject
            ecb.AddComponent(newBuilding, new SpawnHybridBuildingTag
            {
                BuildingItemID = reqData.ItemIDToConsume
            });

            // Создаем запрос на удаление предмета
            // Вместо прямого вызова inventory.Remove()
            var removeItemRequestEntity = ecb.CreateEntity();
            ecb.AddComponent(removeItemRequestEntity, new RemoveItemRequest
            {
                TargetInventoryOwner = playerEntity, // Указываем, что удалить нужно у игрока
                ItemID = reqData.ItemIDToConsume,
                Amount = 1
            });

            // Уничтожаем обработанный запрос на постройку
            ecb.DestroyEntity(requestEntity);
        }
    }
}