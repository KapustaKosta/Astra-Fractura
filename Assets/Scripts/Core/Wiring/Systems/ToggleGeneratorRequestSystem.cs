using Unity.Burst;
using Unity.Entities;
using Energy.Core;

namespace Energy.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ToggleGeneratorRequestSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ToggleGeneratorRequest>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                               .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (req, e) in SystemAPI.Query<RefRO<ToggleGeneratorRequest>>().WithEntityAccess())
            {
                if (state.EntityManager.Exists(req.ValueRO.Target) &&
                    state.EntityManager.HasComponent<GeneratorComponent>(req.ValueRO.Target))
                {
                    var g = state.EntityManager.GetComponentData<GeneratorComponent>(req.ValueRO.Target);


                    g.IsOnline = req.ValueRO.DesiredOn;


                    state.EntityManager.SetComponentData(req.ValueRO.Target, g);
                }
                ecb.DestroyEntity(e);
            }
        }
    }
}