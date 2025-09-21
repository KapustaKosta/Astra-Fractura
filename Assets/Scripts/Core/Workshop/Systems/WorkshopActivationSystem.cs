using Energy.Core;
using Game.Production;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Game.Workshop
{
    /// <summary>
    /// Система, которая активирует производственный процесс в цехе после того,
    /// как планировщик подтвердил его выполнимость. Также распределяет свободных
    /// рабочих по станкам, требующим ручного труда, но не имеющим конкретного назначения.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(WorkshopPlannerSystem))]
    public partial struct WorkshopActivationSystem : ISystem
    {
        /// <summary>
        /// Представляет задачу для станка, требующую ручного труда. Сравнивается по стоимости (HammerCost).
        /// </summary>
        private struct StationTask : System.IComparable<StationTask>
        {
            public Entity StationEntity;
            public float HammerCost;
            public int CompareTo(StationTask other) => other.HammerCost.CompareTo(HammerCost);
        }

        /// <summary>
        /// Представляет доступного для назначения рабочего.
        /// </summary>
        private struct AvailableWorker
        {
            public Entity NpcEntity;
            public float CurrentHammerPool;
        }

        /// <summary>
        /// Основной метод обновления. Находит цеха с выполнимым планом, но еще не активные.
        /// Собирает списки доступных рабочих и задач. Затем распределяет задачи между рабочими,
        /// стараясь сбалансировать нагрузку. После этого активирует цех, добавляя тег ProductionActiveTag.
        /// </summary>
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            var nameLookup = SystemAPI.GetComponentLookup<NetworkNode>(true);
            var stationStateLookup = SystemAPI.GetComponentLookup<StationState>();
            var workForceLookup = SystemAPI.GetComponentLookup<NPCWorkForce>(true);
            var recipeBlob = SystemAPI.GetSingleton<ProductionRecipeRegistryData>().Blob;
            if (!recipeBlob.IsCreated) return;
            ref var registry = ref recipeBlob.Value;

            foreach (var (tag, workers, slots, entity) in SystemAPI.Query<RefRO<WorkshopTag>, DynamicBuffer<AssignedWorker>, DynamicBuffer<StationSlot>>()
                .WithAll<ProductionPlanIsFeasibleTag>()
                .WithNone<ProductionActiveTag>()
                .WithEntityAccess())
            {
                var availableWorkers = new NativeList<AvailableWorker>(Allocator.Temp);
                var unassignedTasks = new NativeList<StationTask>(Allocator.Temp);
                var workersTakenBySpecificAssignment = new NativeHashSet<Entity>(workers.Length, Allocator.Temp);

                foreach (var slot in slots)
                {
                    if (stationStateLookup.HasComponent(slot.Station))
                    {
                        var st = stationStateLookup.GetRefRW(slot.Station);
                        st.ValueRW.AssignedWorker = Entity.Null;
                        if (st.ValueRO.SpecificWorker != Entity.Null)
                        {
                            workersTakenBySpecificAssignment.Add(st.ValueRO.SpecificWorker);
                        }
                        if (st.ValueRO.Enabled == 1 && st.ValueRO.Status != StationStatus.Empty)
                        {
                            st.ValueRW.Status = StationStatus.Idle;
                        }
                    }
                }

                foreach (var worker in workers)
                {
                    if (!workersTakenBySpecificAssignment.Contains(worker.NpcEntity) && workForceLookup.HasComponent(worker.NpcEntity))
                    {
                        availableWorkers.Add(new AvailableWorker
                        {
                            NpcEntity = worker.NpcEntity,
                            CurrentHammerPool = workForceLookup[worker.NpcEntity].CurrentHammerPool
                        });
                    }
                }

                foreach (var slot in slots)
                {
                    if (stationStateLookup.HasComponent(slot.Station))
                    {
                        var st = stationStateLookup.GetRefRO(slot.Station);
                        if (st.ValueRO.Enabled == 1 && st.ValueRO.SpecificWorker == Entity.Null)
                        {
                            ref var recipe = ref FindRecipe(ref registry, st.ValueRO.SelectedRecipeID);
                            if (recipe.RecipeID != -1 && recipe.HammerCost > 0)
                            {
                                unassignedTasks.Add(new StationTask { StationEntity = slot.Station, HammerCost = recipe.HammerCost });
                            }
                        }
                    }
                }

                if (!unassignedTasks.IsEmpty && !availableWorkers.IsEmpty)
                {
                    var workerLoads = new NativeArray<float>(availableWorkers.Length, Allocator.Temp);
                    unassignedTasks.Sort();

                    foreach (var task in unassignedTasks)
                    {
                        int bestWorkerIndex = -1;
                        float minLoad = float.MaxValue;
                        for (int i = 0; i < availableWorkers.Length; i++)
                        {
                            if (workerLoads[i] < minLoad)
                            {
                                minLoad = workerLoads[i];
                                bestWorkerIndex = i;
                            }
                        }

                        if (bestWorkerIndex != -1)
                        {
                            var st = stationStateLookup.GetRefRW(task.StationEntity);
                            st.ValueRW.SpecificWorker = availableWorkers[bestWorkerIndex].NpcEntity;
                            workerLoads[bestWorkerIndex] += task.HammerCost;
                        }
                    }
                    workerLoads.Dispose();
                }

                workersTakenBySpecificAssignment.Dispose();
                availableWorkers.Dispose();
                unassignedTasks.Dispose();

                ecb.AddComponent(entity, new ProductionActiveTag());
                ecb.RemoveComponent<ProductionPlanIsFeasibleTag>(entity);
            }
        }

        /// <summary>
        /// Вспомогательный метод для поиска рецепта по его ID в реестре.
        /// </summary>
        private static ref ProductionRecipe FindRecipe(ref ProductionRecipeRegistryBlob registry, int recipeID)
        {
            for (int i = 0; i < registry.Recipes.Length; i++) if (registry.Recipes[i].RecipeID == recipeID) return ref registry.Recipes[i];
            return ref registry.Recipes[0];
        }
    }
}