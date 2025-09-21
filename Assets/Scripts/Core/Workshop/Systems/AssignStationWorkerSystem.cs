using Unity.Entities;
using Game.Workshop;

/// <summary>
/// Обрабатывает запросы на назначение и снятие NPC с конкретных станков в цехе.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class AssignStationWorkerSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);

        // Обработка запроса НА НАЗНАЧЕНИЕ
        Entities.ForEach((Entity requestEntity, in AssignWorkerToStationRequest request) =>
        {
            if (SystemAPI.Exists(request.NpcEntity) && SystemAPI.Exists(request.Workshop))
            {
                var slots = SystemAPI.GetBuffer<StationSlot>(request.Workshop);
                if (request.SlotIndex >= 0 && request.SlotIndex < slots.Length)
                {
                    var stationEntity = slots[request.SlotIndex].Station;
                    if (SystemAPI.HasComponent<StationState>(stationEntity))
                    {
                        // 1. Назначаем NPC на станок
                        var stationState = SystemAPI.GetComponentRW<StationState>(stationEntity);
                        stationState.ValueRW.SpecificWorker = request.NpcEntity;

                        // 2. Убеждаемся, что NPC привязан к цеху в целом
                        var npcData = SystemAPI.GetComponentRW<NPCComponent>(request.NpcEntity);
                        npcData.ValueRW.AssignedWorkshop = request.Workshop;

                        var workers = SystemAPI.GetBuffer<AssignedWorker>(request.Workshop);
                        bool alreadyInWorkshop = false;
                        foreach (var worker in workers)
                        {
                            if (worker.NpcEntity == request.NpcEntity)
                            {
                                alreadyInWorkshop = true;
                                break;
                            }
                        }

                        if (!alreadyInWorkshop)
                        {
                            workers.Add(new AssignedWorker { NpcEntity = request.NpcEntity });
                        }
                    }
                }
            }
            ecb.DestroyEntity(requestEntity);
        }).Run();

        // Обработка запроса на снятие
        Entities.ForEach((Entity requestEntity, in UnassignWorkerFromStationRequest request) =>
        {
            if (SystemAPI.Exists(request.Workshop))
            {
                var slots = SystemAPI.GetBuffer<StationSlot>(request.Workshop);
                if (request.SlotIndex >= 0 && request.SlotIndex < slots.Length)
                {
                    var stationEntity = slots[request.SlotIndex].Station;
                    if (SystemAPI.HasComponent<StationState>(stationEntity))
                    {
                        // Просто очищаем поле SpecificWorker. NPC остается в общем пуле цеха.
                        var stationState = SystemAPI.GetComponentRW<StationState>(stationEntity);
                        stationState.ValueRW.SpecificWorker = Entity.Null;
                    }
                }
            }
            ecb.DestroyEntity(requestEntity);
        }).Run();
    }
}