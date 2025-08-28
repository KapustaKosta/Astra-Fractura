using Unity.Entities;
using Game.Workshop;

/// <summary>
/// Обрабатывает запросы на назначение NPC для обслуживания цеха.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class AssignWorkerSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);

        Entities.ForEach((Entity requestEntity, in AssignWorkerToWorkshopRequest request) =>
        {
            if (SystemAPI.Exists(request.NpcEntity) && SystemAPI.Exists(request.WorkshopEntity))
            {
                // Привязываем цех к NPC
                var npcData = SystemAPI.GetComponentRW<NPCComponent>(request.NpcEntity);
                npcData.ValueRW.AssignedWorkshop = request.WorkshopEntity;

                // Привязываем NPC к цеху
                if (!SystemAPI.HasBuffer<AssignedWorker>(request.WorkshopEntity))
                {
                    ecb.AddBuffer<AssignedWorker>(request.WorkshopEntity);
                }
                var workers = SystemAPI.GetBuffer<AssignedWorker>(request.WorkshopEntity);
                // Проверяем, что NPC еще не в списке
                bool alreadyAssigned = false;
                foreach (var worker in workers)
                {
                    if (worker.NpcEntity == request.NpcEntity)
                    {
                        alreadyAssigned = true;
                        break;
                    }
                }

                if (!alreadyAssigned)
                {
                    workers.Add(new AssignedWorker { NpcEntity = request.NpcEntity });
                }
            }
            ecb.DestroyEntity(requestEntity);

        }).Run();
    }
}