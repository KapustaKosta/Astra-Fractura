using Game.Workshop;
using Unity.Entities;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(WorkshopWorkerSystem))]
public partial class MaintenanceConditionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Создаем EntityCommandBuffer для безопасного добавления/удаления компонентов
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);

        var isProductionActiveLookup = GetComponentLookup<ProductionActiveTag>(true);
        var stationStateLookup = GetComponentLookup<StationState>(true);
        var stationSlotLookup = GetBufferLookup<StationSlot>(true);


        Entities
            .WithReadOnly(isProductionActiveLookup)
            .WithReadOnly(stationStateLookup)
            .WithReadOnly(stationSlotLookup)
            .ForEach((Entity entity, in NPCComponent npc) =>
            {
                var workshopEntity = npc.AssignedWorkshop;
                bool isWorkAvailable = false;

                // Та же логика обнаружения работы, что и раньше
                if (workshopEntity != Entity.Null
                    && isProductionActiveLookup.HasComponent(workshopEntity)
                    && stationSlotLookup.HasBuffer(workshopEntity))
                {
                    var slots = stationSlotLookup[workshopEntity];
                    foreach (var slot in slots)
                    {
                        if (!stationStateLookup.HasComponent(slot.Station)) continue;
                        var st = stationStateLookup[slot.Station];

                        // Если хотя бы одна станция ждет ручного труда, значит работа есть
                        if (st.Status == StationStatus.AwaitingManualLabor)
                        {
                            isWorkAvailable = true;
                            break;
                        }
                    }
                }


                bool hasTaskTag = SystemAPI.HasComponent<HasMaintenanceTaskTag>(entity);

                // Если работа доступна, а у NPC еще нет тега - добавляем его.
                if (isWorkAvailable && !hasTaskTag)
                {
                    ecb.AddComponent<HasMaintenanceTaskTag>(entity);
                }
                // Если работы нет, а тег у NPC все еще висит - убираем его.
                // Это важно, чтобы NPC не пытался идти в цех, когда работа уже выполнена или отменена.
                else if (!isWorkAvailable && hasTaskTag)
                {
                    ecb.RemoveComponent<HasMaintenanceTaskTag>(entity);
                }


            })
            .WithoutBurst() 
            .Run();
    }
}