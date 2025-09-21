using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Game.Production;

namespace Game.Workshop
{
    /// <summary>
    /// Система, динамически назначающая свободных рабочих на станки, ожидающие ручного труда.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(WorkshopInputConsumptionSystem))]
    public partial struct WorkshopWorkerAssignmentSystem : ISystem
    {
        /// <summary>
        /// Представляет задачу для станка, требующую ручного труда. Сравнивается по стоимости (HammerCost).
        /// </summary>
        private struct WorkstationTask : System.IComparable<WorkstationTask>
        {
            public Entity StationEntity;
            public float HammerCost;
            public int CompareTo(WorkstationTask other) => other.HammerCost.CompareTo(HammerCost);
        }

        /// <summary>
        /// Представляет доступного рабочего. Сравнивается по запасу энергии (HammerPool).
        /// </summary>
        private struct AvailableWorker : System.IComparable<AvailableWorker>
        {
            public Entity NpcEntity;
            public float HammerPool;
            public int CompareTo(AvailableWorker other) => other.HammerPool.CompareTo(HammerPool);
        }

        /// <summary>
        /// Вызывается при создании системы для установки необходимых зависимостей.
        /// </summary>
        [BurstCompile]
        public void OnCreate(ref SystemState s)
        {
            s.RequireForUpdate<ProductionRecipeRegistryData>();
        }

        /// <summary>
        /// Основной метод обновления. Для каждого активного цеха определяет станки, ожидающие
        /// ручного труда, и свободных рабочих. Сначала выполняются специфические назначения.
        /// Затем система сортирует задачи по сложности, а рабочих по энергии, и назначает
        /// самых "свежих" рабочих на самые трудоемкие задачи.
        /// </summary>
        [BurstCompile]
        public void OnUpdate(ref SystemState s)
        {
            var blob = SystemAPI.GetSingleton<ProductionRecipeRegistryData>().Blob;
            if (!blob.IsCreated) return;
            ref var registry = ref blob.Value;

            var workForceLookup = SystemAPI.GetComponentLookup<NPCWorkForce>(true);
            var insideBuildingLookup = SystemAPI.GetComponentLookup<InsideBuildingTag>(true);
            var stationStateLookup = SystemAPI.GetComponentLookup<StationState>(false);

            foreach (var (workers, slots, workshopEntity) in SystemAPI.Query<DynamicBuffer<AssignedWorker>, DynamicBuffer<StationSlot>>().WithAll<WorkshopTag, ProductionActiveTag>().WithEntityAccess())
            {
                if (workers.IsEmpty || slots.IsEmpty) continue;

                var busyWorkers = new NativeHashSet<Entity>(workers.Length, Allocator.Temp);
                foreach (var slot in slots)
                {
                    if (stationStateLookup.HasComponent(slot.Station))
                    {
                        var st = stationStateLookup.GetRefRO(slot.Station);
                        if (st.ValueRO.Status == StationStatus.ApplyingManualLabor && st.ValueRO.AssignedWorker != Entity.Null)
                        {
                            busyWorkers.Add(st.ValueRO.AssignedWorker);
                        }
                    }
                }

                var availableTasks = new NativeList<WorkstationTask>(Allocator.Temp);
                var freeGeneralWorkers = new NativeList<AvailableWorker>(Allocator.Temp);

                foreach (var slot in slots)
                {
                    if (stationStateLookup.HasComponent(slot.Station))
                    {
                        var stRW = stationStateLookup.GetRefRW(slot.Station);
                        if (stRW.ValueRO.Enabled == 1 && stRW.ValueRO.Status == StationStatus.AwaitingManualLabor && stRW.ValueRO.SpecificWorker != Entity.Null)
                        {
                            if (!busyWorkers.Contains(stRW.ValueRO.SpecificWorker) && insideBuildingLookup.HasComponent(stRW.ValueRO.SpecificWorker))
                            {
                                stRW.ValueRW.AssignedWorker = stRW.ValueRO.SpecificWorker;
                                stRW.ValueRW.Status = StationStatus.ApplyingManualLabor;
                                busyWorkers.Add(stRW.ValueRO.SpecificWorker);
                            }
                        }
                        else if (stRW.ValueRO.Enabled == 1 && stRW.ValueRO.Status == StationStatus.AwaitingManualLabor && stRW.ValueRO.SpecificWorker == Entity.Null)
                        {
                            ref var recipe = ref FindRecipe(ref registry, stRW.ValueRO.SelectedRecipeID);
                            if (recipe.RecipeID != -1 && recipe.HammerCost > 0)
                            {
                                availableTasks.Add(new WorkstationTask { StationEntity = slot.Station, HammerCost = recipe.HammerCost });
                            }
                        }
                    }
                }

                foreach (var worker in workers)
                {
                    if (!busyWorkers.Contains(worker.NpcEntity) && insideBuildingLookup.HasComponent(worker.NpcEntity))
                    {
                        if (SystemAPI.GetComponent<NPCComponent>(worker.NpcEntity).AssignedWorkshop == workshopEntity)
                        {
                            bool isSpecific = false;
                            foreach (var slot in slots)
                            {
                                if (SystemAPI.GetComponent<StationState>(slot.Station).SpecificWorker == worker.NpcEntity)
                                {
                                    isSpecific = true;
                                    break;
                                }
                            }
                            if (!isSpecific && workForceLookup.HasComponent(worker.NpcEntity))
                            {
                                var wf = workForceLookup.GetRefRO(worker.NpcEntity);
                                freeGeneralWorkers.Add(new AvailableWorker { NpcEntity = worker.NpcEntity, HammerPool = wf.ValueRO.CurrentHammerPool });
                            }
                        }
                    }
                }

                if (availableTasks.IsEmpty || freeGeneralWorkers.IsEmpty)
                {
                    busyWorkers.Dispose();
                    availableTasks.Dispose();
                    freeGeneralWorkers.Dispose();
                    continue;
                }

                availableTasks.Sort();
                freeGeneralWorkers.Sort();

                int assignments = math.min(availableTasks.Length, freeGeneralWorkers.Length);
                for (int i = 0; i < assignments; i++)
                {
                    var task = availableTasks[i];
                    var worker = freeGeneralWorkers[i];

                    var st = stationStateLookup.GetRefRW(task.StationEntity);
                    st.ValueRW.AssignedWorker = worker.NpcEntity;
                    st.ValueRW.Status = StationStatus.ApplyingManualLabor;
                }

                busyWorkers.Dispose();
                availableTasks.Dispose();
                freeGeneralWorkers.Dispose();
            }
        }
        
        /// <summary>
        /// Вспомогательный метод для поиска рецепта по его ID в реестре.
        /// </summary>
        private static ref ProductionRecipe FindRecipe(ref ProductionRecipeRegistryBlob registry, int recipeID)
        {
            for (int i = 0; i < registry.Recipes.Length; i++)
                if (registry.Recipes[i].RecipeID == recipeID) return ref registry.Recipes[i];
            return ref registry.Recipes[0];
        }
    }
}