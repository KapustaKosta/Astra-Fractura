using Unity.Entities;

namespace Game.Workshop
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct WorkshopStopSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                               .CreateCommandBuffer(state.WorldUnmanaged);

            // Ищем все запросы на остановку от UI
            foreach (var (request, entity) in SystemAPI.Query<RefRO<RequestWorkshopStop>>().WithEntityAccess())
            {
                var workshopEntity = request.ValueRO.Workshop;

                // Проверяем, что цех существует и еще не остановлен
                if (SystemAPI.Exists(workshopEntity) && SystemAPI.HasComponent<ProductionActiveTag>(workshopEntity))
                {
                    // Добавляем тег, который обработает WorkshopLifecycleSystem
                    ecb.AddComponent<RequestHaltProduction>(workshopEntity);
                }

                // Уничтожаем обработанный запрос
                ecb.DestroyEntity(entity);
            }
        }
    }
}