using Game.Workshop;
using Unity.Entities;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(WorkshopMaintenanceExecutionSystem))]
public partial class MaintenanceConditionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Создаем EntityCommandBuffer для безопасного добавления/удаления компонентов
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);

        Entities
            .WithAll<ProductionActiveTag>()
            .ForEach((in DynamicBuffer<AssignedWorker> workers, in DynamicBuffer<StationSlot> slots) =>
            {
                foreach (var worker in workers)
                {
                    var npcEntity = worker.NpcEntity;
                    bool isNeeded = false;

                    foreach (var slot in slots)
                    {
                        if (SystemAPI.HasComponent<StationState>(slot.Station))
                        {
                            var state = SystemAPI.GetComponent<StationState>(slot.Station);

                            if (state.SpecificWorker == npcEntity && state.Enabled == 1)
                            {
                                isNeeded = true;
                                break;
                            }
                        }
                    }

                    bool hasTaskTag = SystemAPI.HasComponent<HasMaintenanceTaskTag>(npcEntity);
                    if (isNeeded && !hasTaskTag)
                    {
                        ecb.AddComponent<HasMaintenanceTaskTag>(npcEntity);
                    }
                    else if (!isNeeded && hasTaskTag)
                    {
                        ecb.RemoveComponent<HasMaintenanceTaskTag>(npcEntity);
                    }
                }
            }).Run();

        Entities
            .WithNone<ProductionActiveTag>()
            .ForEach((in DynamicBuffer<AssignedWorker> workers) =>
            {
                foreach (var worker in workers)
                {
                    if (SystemAPI.HasComponent<HasMaintenanceTaskTag>(worker.NpcEntity))
                    {
                        ecb.RemoveComponent<HasMaintenanceTaskTag>(worker.NpcEntity);
                    }
                }
            }).Run();
    }
}