using Energy.Core;
using Game.Production;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace Game.Workshop
{
    /// <summary>
    /// Система, обрабатывающая запросы на запуск производства, как правило, поступающие от UI.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct WorkshopRequestHandlerSystem : ISystem
    {
        /// <summary>
        /// Основной метод обновления. Находит все запросы StartWorkshopProductionRequest,
        /// очищает текущую очередь производства цеха, добавляет новый заказ,
        /// включает цех и инициирует пересчет плана производства.
        /// </summary>
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
                
                ecb.DestroyEntity(reqEntity);
            }
        }
    }
}