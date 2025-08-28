using Unity.Burst;
using Unity.Entities;
using Unity.Rendering;

namespace Wiring
{
    /// <summary>
    /// Управляет видимостью коннекторов проводов (WireConnector).
    /// Показывает их при входе в режим InWirePlacementMode и скрывает при выходе.
    /// Работает с компонентом DisableRendering, который добавляется при создании коннектора.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct WireConnectorVisibilitySystem : ISystem
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

            // Реагируем на режим прокладки проводов
            bool inWireMode = SystemAPI.HasSingleton<InWirePlacementMode>();

            if (inWireMode)
            {
                // В режиме прокладки: ПОКАЗЫВАЕМ все коннекторы, убирая компонент DisableRendering
                foreach (var (connector, entity) in SystemAPI.Query<RefRO<WireConnector>>()
                             .WithAll<DisableRendering>().WithEntityAccess())
                {
                    ecb.RemoveComponent<DisableRendering>(entity);
                }
            }
            else
            {
                // Вне режима: СКРЫВАЕМ все видимые коннекторы, добавляя DisableRendering
                foreach (var (connector, entity) in SystemAPI.Query<RefRO<WireConnector>>()
                             .WithNone<DisableRendering>().WithEntityAccess())
                {
                    ecb.AddComponent<DisableRendering>(entity);
                }
            }
        }
    }
}