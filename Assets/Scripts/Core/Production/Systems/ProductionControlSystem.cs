using Unity.Burst;
using Unity.Entities;

namespace Game.Production
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(ProductionSystem))]
    public partial struct ProductionControlSystem : ISystem
    {
        public void OnUpdate(ref SystemState s)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                               .CreateCommandBuffer(s.WorldUnmanaged);

            foreach (var (req, ent) in SystemAPI.Query<RefRO<StartProductionRequest>>().WithEntityAccess())
            {
                if (SystemAPI.HasBuffer<ProductionQueueItem>(req.ValueRO.Building))
                {
                    var queue = SystemAPI.GetBuffer<ProductionQueueItem>(req.ValueRO.Building);
                    queue.Add(new ProductionQueueItem
                    {
                        RecipeID = req.ValueRO.RecipeID,
                        AmountToProduce = req.ValueRO.Amount,
 
                    });
                }

                if (SystemAPI.HasComponent<ProductionBuildingState>(req.ValueRO.Building))
                {
                    var st = SystemAPI.GetComponent<ProductionBuildingState>(req.ValueRO.Building);
                    st.IsOn = true;
                    SystemAPI.SetComponent(req.ValueRO.Building, st);
                }
                ecb.DestroyEntity(ent);
            }

  
            foreach (var (req, ent) in SystemAPI.Query<RefRO<StopProductionRequest>>().WithEntityAccess())
            {
                var b = req.ValueRO.Building;
                if (SystemAPI.Exists(b))
                {
                    if (SystemAPI.HasBuffer<ProductionQueueItem>(b))
                    {
                        SystemAPI.GetBuffer<ProductionQueueItem>(b).Clear();
                    }
                    if (SystemAPI.HasComponent<ProductionBuildingState>(b))
                    {
                        var st = SystemAPI.GetComponent<ProductionBuildingState>(b);
                        st.IsOn = false;
                        st.RemainingTime = 0;
                        st.ActiveRecipeIndex = -1;
                        st.Status = ProductionStatus.Idle; // Возвращаем в состояние простоя
                        SystemAPI.SetComponent(b, st);
                    }
                }
                ecb.DestroyEntity(ent);
            }


            foreach (var (req, ent) in SystemAPI.Query<RefRO<SetProductionRecipeRequest>>().WithEntityAccess())
            {
                var b = req.ValueRO.Building;
                if (SystemAPI.Exists(b) && SystemAPI.HasComponent<ProductionBuildingState>(b))
                {
                    var st = SystemAPI.GetComponent<ProductionBuildingState>(b);
                    st.SelectedRecipeID = req.ValueRO.RecipeID;
                    SystemAPI.SetComponent(b, st);
                }
                ecb.DestroyEntity(ent);
            }
        }
    }
}