using Unity.Burst;
using Unity.Entities;

namespace Game.Workshop
{
    /// <summary>
    /// Система, обеспечивающая целостность производственной цепочки. Если в активном цехе
    /// происходит изменение (перемещение, удаление станка), эта система инициирует его остановку.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MoveStationRequestSystem))]
    [UpdateBefore(typeof(WorkshopDeactivationSystem))]
    public partial struct WorkshopChainIntegritySystem : ISystem
    {
        /// <summary>
        /// Основной метод обновления. Ищет активные цеха с тегом WorkshopChainChangedTag
        /// и добавляет им компонент RequestHaltProduction для последующей деактивации.
        /// </summary>
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            
            foreach (var (tag, entity) in SystemAPI.Query<RefRO<WorkshopTag>>()
                         .WithAll<ProductionActiveTag, WorkshopChainChangedTag>()
                         .WithEntityAccess())
            {
                ecb.AddComponent<RequestHaltProduction>(entity);
                ecb.RemoveComponent<WorkshopChainChangedTag>(entity);
            }
        }
    }
}