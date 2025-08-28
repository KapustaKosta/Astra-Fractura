using Energy.Core;
using Game.Production;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace Game.Workshop
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct WorkshopRequestHandlerSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            var nameLookup = SystemAPI.GetComponentLookup<NetworkNode>(true);

            // Обработка запроса на СТАРТ от UI
            foreach (var (req, reqEntity) in SystemAPI.Query<RefRO<StartWorkshopProductionRequest>>().WithEntityAccess())
            {
                var workshop = req.ValueRO.Workshop;
                if (!SystemAPI.Exists(workshop)) { ecb.DestroyEntity(reqEntity); continue; }

                // Проверяем, есть ли у цеха компонент состояния, и если да - включаем его.
                if (SystemAPI.HasComponent<WorkshopState>(workshop))
                {
                    // Получаем доступ к состоянию цеха для записи
                    var workshopState = SystemAPI.GetComponentRW<WorkshopState>(workshop);
                    // Включаем цех
                    workshopState.ValueRW.IsOn = true;
                }


                var queue = SystemAPI.GetBuffer<WorkshopProductionQueueItem>(workshop);
                queue.Clear();
                queue.Add(new WorkshopProductionQueueItem
                {
                    FinalRecipeID = req.ValueRO.FinalRecipeID,
                    AmountToProduce = req.ValueRO.Amount,
                    InitialAmount = req.ValueRO.InitialAmount
                });

                ecb.AddComponent(workshop, new RequestRecalculatePlan());

                var name = nameLookup.HasComponent(workshop) ? nameLookup[workshop].Name.ToString() : "Unknown Workshop";
                Debug.Log($"[RequestHandler] UI requested START for '{name}'. Emitting RequestRecalculatePlan.");

                ecb.DestroyEntity(reqEntity);
            }
        }
    }
}