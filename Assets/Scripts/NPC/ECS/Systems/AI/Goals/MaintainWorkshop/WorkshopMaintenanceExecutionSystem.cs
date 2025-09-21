using Unity.Entities;
using Game.Workshop;
using Game.Production;
using Unity.Mathematics;
using Unity.Collections;
using Energy.Core;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class WorkshopMaintenanceExecutionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var entityManager = this.EntityManager;
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        float dt = SystemAPI.Time.DeltaTime;

        var recipeRegistryData = SystemAPI.GetSingleton<ProductionRecipeRegistryData>();
        if (!recipeRegistryData.Blob.IsCreated) return;
        var recipeBlobRef = recipeRegistryData.Blob;

        // Получаем все Lookup-ы в начале
        var stationStateLookup = GetComponentLookup<StationState>(false);
        var ltwLookup = GetComponentLookup<LocalToWorld>(true);
        var arrivalLookup = GetComponentLookup<ArrivalData>(true);

        // Этап 1: NPC прибыл в цех.
        Entities
            .WithStructuralChanges()
            .WithAll<ActiveGoal>().WithNone<InsideBuildingTag, WorkshopMaintenanceInProgress>()
            .WithReadOnly(ltwLookup)
            .WithReadOnly(arrivalLookup)
            .ForEach((Entity npcEntity, ref NPCWorkForce workForce, in NPCMovementComponent movement, in ActiveGoal goal, in LocalToWorld npcTransform) =>
            {
                if (goal.Type != GoalType.MaintainWorkshop) return;

                var workshopEntity = goal.Target;
                if (!entityManager.Exists(workshopEntity) || !entityManager.HasComponent<WorkshopTag>(workshopEntity) || !ltwLookup.HasComponent(workshopEntity)) return;

                bool hasArrived = !movement.HasTarget;

                var workshopTransform = ltwLookup[workshopEntity];
                float3 targetPoint = workshopTransform.Position;
                float targetRadius = 1.5f;

                if (arrivalLookup.HasComponent(workshopEntity))
                {
                    var arrivalData = arrivalLookup[workshopEntity];
                    float3 offset = math.mul(workshopTransform.Rotation, arrivalData.Offset);
                    targetPoint += offset;
                    targetRadius = arrivalData.Radius;
                }

                // Добавляем небольшой запас к радиусу, чтобы компенсировать StoppingDistance
                targetRadius += movement.StoppingDistance;

                bool isInRange = math.distancesq(npcTransform.Position, targetPoint) <= targetRadius * targetRadius;

                if (hasArrived && isInRange)
                {
                    workForce.CurrentHammerPool = workForce.MaxHammerPool;
                    entityManager.AddComponent<InsideBuildingTag>(npcEntity);
                    entityManager.AddComponentData(npcEntity, new WorkshopMaintenanceInProgress { WorkshopEntity = workshopEntity });
                }

            }).Run();


        stationStateLookup.Update(this);

        // Этап 2: NPC находится в цеху и выполняет работу.
        Entities
            .ForEach((Entity npcEntity, ref WorkshopMaintenanceInProgress progress, ref NPCWorkForce workForce) =>
            {
                var workshopEntity = progress.WorkshopEntity;
                if (!SystemAPI.Exists(workshopEntity)) { ecb.RemoveComponent<WorkshopMaintenanceInProgress>(npcEntity); return; }

                bool isWorkStillNeeded = SystemAPI.HasComponent<HasMaintenanceTaskTag>(npcEntity);
                if (!isWorkStillNeeded)
                {
                    ecb.RemoveComponent<WorkshopMaintenanceInProgress>(npcEntity);
                    return;
                }

                if (workForce.CurrentHammerPool <= 0) return;

                var slots = SystemAPI.GetBuffer<StationSlot>(workshopEntity);
                Entity targetStation = Entity.Null;

                foreach (var slot in slots)
                {
                    // Теперь эта проверка будет безопасной
                    if (stationStateLookup.HasComponent(slot.Station))
                    {
                        var state = stationStateLookup[slot.Station];
                        // Ищем нашу назначенную станцию, которая ждет работы
                        if (state.SpecificWorker == npcEntity && state.Status == StationStatus.ApplyingManualLabor)
                        {
                            targetStation = slot.Station;
                            break;
                        }
                    }
                }

                if (targetStation == Entity.Null) return;

                var targetState = stationStateLookup[targetStation];
                ref var recipe = ref FindRecipe(ref recipeBlobRef.Value, targetState.SelectedRecipeID);
                if (recipe.RecipeID == -1) return;

                float workRate = 1.0f; // Работа со скоростью 1 HC в секунду
                float workAmount = math.min(workForce.CurrentHammerPool, dt * workRate * targetState.PowerEfficiency);
                float neededHC = recipe.HammerCost - targetState.AppliedHammerCost;
                workAmount = math.min(workAmount, neededHC);

                if (workAmount > 0)
                {
                    targetState.AppliedHammerCost += workAmount;
                    workForce.CurrentHammerPool -= workAmount;
                }
                
                if ((recipe.HammerCost - targetState.AppliedHammerCost) <= 0.001f)
                {
                    // HC выполнен, переводим станок в состояние Working для отсчета BT
                    targetState.Status = StationStatus.Working;
                    targetState.RemainingTime = recipe.BaseTime + targetState.TimePenalty;
                    targetState.AssignedWorker = Entity.Null; // Освобождаем временное назначение
                }

                stationStateLookup[targetStation] = targetState;

            }).Run();

        // Этап 3: NPC освобожден.
        Entities
            .WithAll<InsideBuildingTag>().WithNone<WorkshopMaintenanceInProgress>()
            .ForEach((Entity npcEntity) =>
            {
                ecb.RemoveComponent<InsideBuildingTag>(npcEntity);
                ecb.AddComponent(npcEntity, new CleanupGoalRequest { OldGoalType = GoalType.MaintainWorkshop });
            }).Run();
    }

    private static ref ProductionRecipe FindRecipe(ref ProductionRecipeRegistryBlob registry, int recipeID)
    {
        for (int i = 0; i < registry.Recipes.Length; i++)
            if (registry.Recipes[i].RecipeID == recipeID) return ref registry.Recipes[i];
        return ref registry.Recipes[0];
    }
}