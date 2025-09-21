using Unity.Burst;
using Unity.Entities;

namespace Game.Workshop
{
    /// <summary>
    /// Система, отвечающая за прогресс производства на работающих станках.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(WorkshopInputConsumptionSystem))]
    public partial struct WorkshopProgressSystem : ISystem
    {
        /// <summary>
        /// Основной метод обновления. Для каждого станка в статусе 'Working',
        /// уменьшает оставшееся время производства (RemainingTime) с учетом
        /// эффективности энергоснабжения (PowerEfficiency).
        /// </summary>
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            foreach (var stationState in SystemAPI.Query<RefRW<StationState>>().WithAll<StationTag>())
            {
                if (stationState.ValueRO.Status == StationStatus.Working && stationState.ValueRO.PowerEfficiency > 0.001f)
                {
                    stationState.ValueRW.RemainingTime -= dt * stationState.ValueRO.PowerEfficiency;
                }
            }
        }
    }
}