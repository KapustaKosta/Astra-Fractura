using Unity.Burst;
using Unity.Entities;

namespace Conveyor
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ConnectorVisibilitySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                               .CreateCommandBuffer(state.WorldUnmanaged);

            bool inConveyorMode = SystemAPI.HasSingleton<InConveyorMode>();

            if (inConveyorMode)
            {
                // РЕЖИМ СТРОИТЕЛЬСТВА:
                // 1. Показываем все СВОБОДНЫЕ коннекторы (у кого нет Disabled и Occupied).
                foreach (var (conn, entity) in SystemAPI.Query<RefRO<ConveyorConnector>>()
                             .WithAll<Disabled>().WithNone<ConveyorOccupiedTag>().WithEntityAccess())
                {
                    ecb.RemoveComponent<Disabled>(entity);
                }

                // 2. Гарантированно скрываем все ЗАНЯТЫЕ коннекторы (у кого есть Occupied, но нет Disabled).
                foreach (var (conn, entity) in SystemAPI.Query<RefRO<ConveyorConnector>>()
                             .WithAll<ConveyorOccupiedTag>().WithNone<Disabled>().WithEntityAccess())
                {
                    ecb.AddComponent<Disabled>(entity);
                }
            }
            else
            {
                // ВНЕ РЕЖИМА СТРОИТЕЛЬСТВА: Скрываем абсолютно все коннекторы.
                foreach (var (conn, entity) in SystemAPI.Query<RefRO<ConveyorConnector>>()
                             .WithNone<Disabled>().WithEntityAccess())
                {
                    ecb.AddComponent<Disabled>(entity);
                }
            }
        }
    }
}