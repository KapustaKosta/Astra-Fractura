using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

/// <summary>
/// Система, которая финализирует размещение конвеера.
/// <para>
/// - Обрабатывает только запросы на размещение конвеера (PlaceBuildingRequest для ConveyorBeltTag).
/// - Находит ближайшие точки подключения (endpoints) для старта и конца конвеера.
/// - Инстанцирует сущность конвеера, соединяет её с найденными точками.
/// - Помечает точки как занятые тегом ConveyorEndpointOccupiedTag.
/// - Удаляет запрос на размещение после обработки.
/// </para>
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
[UpdateAfter(typeof(FinalizeBuildingSystem))]
public partial class ConveyorFinalizeSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach (var (request, requestEntity) in SystemAPI.Query<RefRO<PlaceBuildingRequest>>().WithEntityAccess())
        {
            var reqData = request.ValueRO;
            if (!SystemAPI.HasComponent<ConveyorBeltTag>(reqData.BuildingPrefabToPlace))
                continue;

            Entity start = Entity.Null, end = Entity.Null;
            float minStart = float.MaxValue, minEnd = float.MaxValue;
            foreach (var (endpoint, entity) in SystemAPI.Query<RefRO<ConveyorEndpoint>>().WithEntityAccess())
            {
                var endpointTransform = SystemAPI.GetComponent<LocalTransform>(entity);
                float dist = math.distance(endpointTransform.Position, reqData.Position);
                if (!endpoint.ValueRO.IsInput && dist < minStart)
                {
                    minStart = dist;
                    start = entity;
                }
                if (endpoint.ValueRO.IsInput && dist < minEnd)
                {
                    minEnd = dist;
                    end = entity;
                }
            }
            if (start == Entity.Null || end == Entity.Null || start == end)
            {
                continue;
            }

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
            ecb.AddComponent<ConveyorEndpointOccupiedTag>(start);
            ecb.AddComponent<ConveyorEndpointOccupiedTag>(end);

            var conveyor = ecb.Instantiate(reqData.BuildingPrefabToPlace);
            ecb.SetComponent(conveyor, LocalTransform.FromPositionRotation(reqData.Position, reqData.Rotation));
            var startTransform = SystemAPI.GetComponent<LocalTransform>(start);
            var endTransform = SystemAPI.GetComponent<LocalTransform>(end);
            ecb.AddComponent(conveyor, new ConveyorComponent
            {
                StartEntity = start,
                EndEntity = end,
                Direction = math.normalize(endTransform.Position - startTransform.Position),
                Length = math.distance(startTransform.Position, endTransform.Position)
            });
            ecb.AddBuffer<ConveyorResourceBuffer>(conveyor);
            ecb.AddComponent<NewlyBuiltTag>(conveyor);
            ecb.DestroyEntity(requestEntity);
        }
    }
}
