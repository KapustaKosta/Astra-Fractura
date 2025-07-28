using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

/// <summary>
/// Система, которая финализирует размещение конвеера, создаёт сущность конвеера и соединяет его с точками зданий.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
[UpdateAfter(typeof(FinalizeBuildingSystem))]
public partial class ConveyorFinalizeSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Обрабатываем только запросы на размещение конвеера
        foreach (var (request, requestEntity) in SystemAPI.Query<RefRO<PlaceBuildingRequest>>().WithEntityAccess())
        {
            var reqData = request.ValueRO;
            // Проверяем, что это префаб конвеера (по тегу ConveyorBeltTag)
            if (!SystemAPI.HasComponent<ConveyorBeltTag>(reqData.BuildingPrefabToPlace))
                continue;

            // Получаем точки подключения (должны быть определены в BuildingState или через отдельный запрос)
            // Здесь предполагается, что в PlaceBuildingRequest можно добавить поля StartEndpoint/EndEndpoint
            // Для простоты примера ? ищем ближайшие точки
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
                // Не удалось найти валидные точки ? пропускаем
                continue;
            }

            // Инстанцируем конвеер
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
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
            // Удаляем запрос
            ecb.DestroyEntity(requestEntity);
        }
    }
}
