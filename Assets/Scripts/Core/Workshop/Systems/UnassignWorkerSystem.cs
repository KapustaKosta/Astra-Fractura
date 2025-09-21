using Unity.Entities;
using Game.Workshop;

/// <summary>
/// Обрабатывает запросы на полное снятие NPC с цеха.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class UnassignWorkerSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);

        Entities.ForEach((Entity requestEntity, in UnassignWorkerFromWorkshopRequest request) =>
        {
            if (SystemAPI.Exists(request.NpcEntity) && SystemAPI.Exists(request.Workshop))
            {
                // 1. Убираем NPC из общего пула рабочих цеха
                var workers = SystemAPI.GetBuffer<AssignedWorker>(request.Workshop);
                for (int i = workers.Length - 1; i >= 0; i--)
                {
                    if (workers[i].NpcEntity == request.NpcEntity)
                    {
                        workers.RemoveAt(i);
                        break;
                    }
                }

                // 2. Убираем привязку к цеху у самого NPC
                var npcData = SystemAPI.GetComponentRW<NPCComponent>(request.NpcEntity);
                if (npcData.ValueRO.AssignedWorkshop == request.Workshop)
                {
                    npcData.ValueRW.AssignedWorkshop = Entity.Null;
                }

                // 3. Проверяем все станки этого цеха и снимаем NPC, если он был назначен на какой-то конкретно
                if (SystemAPI.HasBuffer<StationSlot>(request.Workshop))
                {
                    var slots = SystemAPI.GetBuffer<StationSlot>(request.Workshop);
                    foreach (var slot in slots)
                    {
                        if (SystemAPI.HasComponent<StationState>(slot.Station))
                        {
                            var stationState = SystemAPI.GetComponentRW<StationState>(slot.Station);
                            if (stationState.ValueRO.SpecificWorker == request.NpcEntity)
                            {
                                stationState.ValueRW.SpecificWorker = Entity.Null;
                            }
                        }
                    }
                }
            }
            ecb.DestroyEntity(requestEntity);
        }).Run();
    }
}