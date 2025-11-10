using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Game.Production;

namespace Game.Research
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ResearchPointGenerationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ResearchState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var playerEntity = SystemAPI.GetSingletonEntity<ResearchState>();
            var accumulator = SystemAPI.GetComponentRW<ResearchPointAccumulator>(playerEntity);
            var researchState = SystemAPI.GetComponentRW<ResearchState>(playerEntity);

            double now = SystemAPI.Time.ElapsedTime;
            if (accumulator.ValueRW.LastTickTime <= 0d)
            {
                accumulator.ValueRW.LastTickTime = now;
            }

            double elapsed = now - accumulator.ValueRW.LastTickTime;
            if (elapsed < 1d)
            {
                return;
            }

            int wholeSeconds = (int)math.floor(elapsed);
            accumulator.ValueRW.LastTickTime += wholeSeconds;

            float totalRate = 0f;
            foreach (var (source, buildingState) in SystemAPI.Query<RefRO<ResearchPointSource>, RefRO<ProductionBuildingState>>())
            {
                if (buildingState.ValueRO.IsOn)
                {
                    totalRate += math.max(0f, source.ValueRO.PointsPerSecond);
                }
            }

            foreach (var source in SystemAPI.Query<RefRO<ResearchPointSource>>().WithAbsent<ProductionBuildingState>())
            {
                totalRate += math.max(0f, source.ValueRO.PointsPerSecond);
            }

            float gained = totalRate * wholeSeconds;
            float combined = gained + accumulator.ValueRW.FractionalRemainder;
            int gainedInt = (int)math.floor(combined + 1e-3f);
            accumulator.ValueRW.FractionalRemainder = combined - gainedInt;

            if (gainedInt > 0)
            {
                researchState.ValueRW.ResearchPoints += gainedInt;
                if (!SystemAPI.HasComponent<ResearchStateDirty>(playerEntity))
                {
                    state.EntityManager.AddComponent<ResearchStateDirty>(playerEntity);
                }
            }
        }
    }
}
