using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.Rendering;

/// <summary>
/// Финальная система, которая обрабатывает запросы PlaceBuildingRequest,
/// инстанциирует здание и создает запрос на удаление предмета из инвентаря игрока.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
public partial class FinalizeBuildingSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(World.Unmanaged);

        // Получаем сущность игрока, чтобы знать, из чьего инвентаря удалять предмет.
        if (!SystemAPI.TryGetSingletonEntity<PlayerControllerData>(out var playerEntity))
        {
            var q = SystemAPI.QueryBuilder().WithAll<PlaceBuildingRequest>().Build();
            ecb.DestroyEntity(q, EntityQueryCaptureMode.AtPlayback);
            return;
        }

        foreach (var (request, reqEntity) in SystemAPI.Query<RefRO<PlaceBuildingRequest>>().WithEntityAccess())
        {
            var req = request.ValueRO;
            if (req.BuildingPrefabToPlace == Entity.Null)
            {
                ecb.DestroyEntity(reqEntity);
                continue;
            }

            var newBuilding = ecb.Instantiate(req.BuildingPrefabToPlace);


            // 1. Читаем масштаб из оригинального префаба, чтобы не потерять его.
            float prefabScale = 1f;
            if (SystemAPI.HasComponent<LocalTransform>(req.BuildingPrefabToPlace))
            {
                prefabScale = SystemAPI.GetComponent<LocalTransform>(req.BuildingPrefabToPlace).Scale;
            }

            // 2. Используем метод, который сохраняет масштаб.
            ecb.SetComponent(newBuilding, LocalTransform.FromPositionRotationScale(req.Position, req.Rotation, prefabScale));


            ecb.AddComponent<NewlyBuiltTag>(newBuilding);
            ecb.AddComponent(newBuilding, new SpawnHybridBuildingTag { BuildingItemID = req.ItemIDToConsume });

            var removeReq = ecb.CreateEntity();
            ecb.AddComponent(removeReq, new RemoveItemRequest
            {
                TargetInventoryOwner = playerEntity,
                ItemID = req.ItemIDToConsume,
                Amount = 1
            });

            ecb.DestroyEntity(reqEntity);
        }
    }
}