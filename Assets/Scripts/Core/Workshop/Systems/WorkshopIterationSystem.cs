using Game.Workshop;
using Unity.Entities;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(WorkshopWorkerSystem))] 
public partial struct WorkshopIterationSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        var workForceLookup = SystemAPI.GetComponentLookup<NPCWorkForce>(false);

        // Проходим по всем активным цехам
        foreach (var (slots, workers, entity) in SystemAPI.Query<DynamicBuffer<StationSlot>, DynamicBuffer<AssignedWorker>>()
            .WithAll<WorkshopTag, ProductionActiveTag>().WithEntityAccess())
        {
            bool isManualLaborInProgress = false;
            // Проверяем, есть ли хоть один станок, который требует или обрабатывается вручную
            foreach (var slot in slots)
            {
                if (SystemAPI.HasComponent<StationState>(slot.Station))
                {
                    var stationStatus = SystemAPI.GetComponent<StationState>(slot.Station).Status;
                    if (stationStatus == StationStatus.AwaitingManualLabor || stationStatus == StationStatus.ApplyingManualLabor)
                    {
                        isManualLaborInProgress = true;
                        break;
                    }
                }
            }

            bool wasPassComplete = SystemAPI.HasComponent<WorkshopPassCompleteTag>(entity);

            // ИТЕРАЦИЯ ТОЛЬКО ЧТО ЗАВЕРШИЛАСЬ
            // Ручной работы больше нет, но система еще не пометила этот проход как завершенный.
            if (!isManualLaborInProgress && !wasPassComplete)
            {
                // Восстанавливаем HC всем рабочим этого цеха
                foreach (var worker in workers)
                {
                    if (workForceLookup.HasComponent(worker.NpcEntity))
                    {
                        var wf = workForceLookup.GetRefRW(worker.NpcEntity);
                        wf.ValueRW.CurrentHammerPool = wf.ValueRO.MaxHammerPool;
                    }
                }

                // Ставим тег, чтобы не восстанавливать HC каждый кадр
                ecb.AddComponent(entity, new WorkshopPassCompleteTag());
            }
            // НАЧАЛАСЬ НОВАЯ ИТЕРАЦИЯ
            // Появилась новая работа, а прошлый проход был помечен как завершенный.
            else if (isManualLaborInProgress && wasPassComplete)
            {
                // Снимаем тег, чтобы система была готова к следующему завершению итерации
                ecb.RemoveComponent<WorkshopPassCompleteTag>(entity);
            }
        }
    }
}