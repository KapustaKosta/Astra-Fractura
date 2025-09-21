using Unity.Burst;
using Unity.Entities;

namespace Game.Workshop
{
    /// <summary>
    /// Система, обрабатывающая запросы на включение или выключение всего цеха.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(WorkshopActivationSystem))]
    public partial struct ToggleWorkshopRequestSystem : ISystem
    {
        /// <summary>
        /// Основной метод обновления. Находит все запросы ToggleWorkshopRequest
        /// и устанавливает флаг IsOn в компоненте WorkshopState.
        /// </summary>
        [BurstCompile]
        public void OnUpdate(ref SystemState s)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(s.WorldUnmanaged);
            foreach (var (req, ent) in SystemAPI.Query<RefRO<ToggleWorkshopRequest>>().WithEntityAccess())
            {
                if (SystemAPI.Exists(req.ValueRO.Workshop) &&
                    SystemAPI.HasComponent<WorkshopState>(req.ValueRO.Workshop))
                {
                    var st = SystemAPI.GetComponent<WorkshopState>(req.ValueRO.Workshop);
                    st.IsOn = req.ValueRO.Enable;
                    SystemAPI.SetComponent(req.ValueRO.Workshop, st);
                    ecb.AddComponent<WorkshopChainChangedTag>(req.ValueRO.Workshop);
                }

                ecb.DestroyEntity(ent);
            }
        }
    }
}