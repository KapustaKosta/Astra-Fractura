using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Game.Production;

namespace Game.Workshop
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(WorkshopSystem))]
    public partial struct WorkshopWorkerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState s)
        {
            s.RequireForUpdate<ProductionRecipeRegistryData>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState s)
        {
            var blob = SystemAPI.GetSingleton<ProductionRecipeRegistryData>().Blob;
            if (!blob.IsCreated) return;
            ref var registry = ref blob.Value;
            float dt = SystemAPI.Time.DeltaTime;

            var stationStateLookup = SystemAPI.GetComponentLookup<StationState>(false);
            var workForceLookup = SystemAPI.GetComponentLookup<NPCWorkForce>(false);


            // Получаем доступ к тегу, чтобы проверить, находится ли NPC внутри
            var insideBuildingLookup = SystemAPI.GetComponentLookup<InsideBuildingTag>(true);



            // Этот первый цикл остается без изменений, он обрабатывает уже работающих NPC
            foreach (var (stationState, stationEntity) in SystemAPI.Query<RefRW<StationState>>().WithAll<StationTag>().WithEntityAccess())
            {
                if (stationState.ValueRO.Status != StationStatus.ApplyingManualLabor || stationState.ValueRO.AssignedWorker == Entity.Null) continue;

                var workerEntity = stationState.ValueRO.AssignedWorker;

                if (!workForceLookup.HasComponent(workerEntity) || !SystemAPI.HasComponent<InsideBuildingTag>(workerEntity))
                {
                    continue;
                }

                var workForce = workForceLookup.GetRefRW(workerEntity);
                ref var recipe = ref FindRecipe(ref registry, stationState.ValueRO.SelectedRecipeID);
                if (recipe.RecipeID == -1) continue;

                float totalHCNeded = recipe.HammerCost;
                float workThisFrame = 1.0f * dt;
                workThisFrame = math.min(workThisFrame, workForce.ValueRO.CurrentHammerPool);
                float remainingHC = totalHCNeded - stationState.ValueRO.AppliedHammerCost;
                workThisFrame = math.min(workThisFrame, remainingHC);

                if (workThisFrame > 0f)
                {
                    stationState.ValueRW.AppliedHammerCost += workThisFrame;
                    workForce.ValueRW.CurrentHammerPool -= workThisFrame;
                }

                remainingHC = totalHCNeded - stationState.ValueRO.AppliedHammerCost;

                if (remainingHC <= 0.001f)
                {
                    stationState.ValueRW.Status = StationStatus.Working;
                    stationState.ValueRW.RemainingTime = recipe.BaseTime + stationState.ValueRO.TimePenalty;
                    stationState.ValueRW.AssignedWorker = Entity.Null;
                }
                else if (workForce.ValueRO.CurrentHammerPool <= 0.001f)
                {
                    stationState.ValueRW.TimePenalty += remainingHC * 2.0f;
                    stationState.ValueRW.Status = StationStatus.Working;
                    stationState.ValueRW.RemainingTime = recipe.BaseTime + stationState.ValueRO.TimePenalty;
                    stationState.ValueRW.AssignedWorker = Entity.Null;
                }
            }


            foreach (var (workers, workshopEntity) in SystemAPI.Query<DynamicBuffer<AssignedWorker>>().WithAll<WorkshopTag>().WithEntityAccess())
            {
                if (workers.IsEmpty) continue;

                var freeWorkers = new NativeList<Entity>(Allocator.Temp);
                var busyWorkers = new NativeHashSet<Entity>(workers.Length, Allocator.Temp);

                foreach (var stationState in SystemAPI.Query<RefRO<StationState>>())
                {
                    if (stationState.ValueRO.AssignedWorker != Entity.Null)
                    {
                        busyWorkers.Add(stationState.ValueRO.AssignedWorker);
                    }
                }

                foreach (var worker in workers)
                {

                    if (!busyWorkers.Contains(worker.NpcEntity) && insideBuildingLookup.HasComponent(worker.NpcEntity))
                    {
                        freeWorkers.Add(worker.NpcEntity);
                    }

                }
                busyWorkers.Dispose();
                if (freeWorkers.IsEmpty) { freeWorkers.Dispose(); continue; }

                var slots = SystemAPI.GetBuffer<StationSlot>(workshopEntity);
                for (int i = 0; i < slots.Length && !freeWorkers.IsEmpty; i++)
                {
                    var stEnt = slots[i].Station;
                    if (stationStateLookup.HasComponent(stEnt))
                    {
                        var st = stationStateLookup.GetRefRW(stEnt);
                        if (st.ValueRO.Enabled == 1 && st.ValueRO.Status == StationStatus.AwaitingManualLabor && st.ValueRO.AssignedWorker == Entity.Null)
                        {
                            st.ValueRW.AssignedWorker = freeWorkers[0];
                            st.ValueRW.Status = StationStatus.ApplyingManualLabor;
                            freeWorkers.RemoveAt(0);
                        }
                    }
                }
                freeWorkers.Dispose();
            }
        }

        private static ref ProductionRecipe FindRecipe(ref ProductionRecipeRegistryBlob registry, int recipeID)
        {
            for (int i = 0; i < registry.Recipes.Length; i++)
                if (registry.Recipes[i].RecipeID == recipeID) return ref registry.Recipes[i];
            return ref registry.Recipes[0];
        }
    }
}