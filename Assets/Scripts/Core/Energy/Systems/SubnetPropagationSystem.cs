using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Energy.Core;

namespace Energy.Core.Systems
{
    /// <summary>
    /// Копирует SubnetId от родителя всем дочерним сущностям с NetworkNode.
    /// Этим «догоняем» генераторы/нагрузки/батареи, которые являются детьми корневых узлов.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(NetworkDiscoverySystem))]
    public partial struct SubnetPropagationSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { }

        public void OnUpdate(ref SystemState state)
        {
            var parentNodes = SystemAPI.GetComponentLookup<NetworkNode>(true);

            foreach (var (childNode, parent) in SystemAPI
                         .Query<RefRW<NetworkNode>, RefRO<Parent>>())
            {
                Entity p = parent.ValueRO.Value;
                if (parentNodes.HasComponent(p))
                {
                    var pn = parentNodes[p];
                    childNode.ValueRW.SubnetId = pn.SubnetId;
                }
            }
        }
    }
}
