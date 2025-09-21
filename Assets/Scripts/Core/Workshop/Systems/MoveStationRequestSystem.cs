using Unity.Burst;
using Unity.Entities;

namespace Game.Workshop
{
    /// <summary>
    /// Система, обрабатывающая запросы на перемещение (смену мест) станций внутри цеха.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(WorkshopActivationSystem))]
    public partial struct MoveStationRequestSystem : ISystem
    {
        /// <summary>
        /// Основной метод обновления. Обрабатывает запросы MoveStationRequest,
        /// проверяет валидность индексов и меняет местами соответствующие
        /// элементы в буфере StationSlot цеха.
        /// </summary>
        [BurstCompile]
        public void OnUpdate(ref SystemState s)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(s.WorldUnmanaged);
            foreach (var (req, ent) in SystemAPI.Query<RefRO<MoveStationRequest>>().WithEntityAccess())
            {
                if (!SystemAPI.Exists(req.ValueRO.Workshop) || !SystemAPI.HasBuffer<StationSlot>(req.ValueRO.Workshop))
                { ecb.DestroyEntity(ent); continue; }

                ecb.AddComponent<WorkshopChainChangedTag>(req.ValueRO.Workshop);

                var slots = SystemAPI.GetBuffer<StationSlot>(req.ValueRO.Workshop);
                int a = req.ValueRO.FromIndex, b = req.ValueRO.ToIndex;
                if (a < 0 || b < 0 || a >= slots.Length || b >= slots.Length || a == b) { ecb.DestroyEntity(ent); continue; }

                var tempA = slots[a];
                slots[a] = slots[b];
                slots[b] = tempA;

                ecb.DestroyEntity(ent);
            }
        }
    }
}