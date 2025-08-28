using Unity.Burst;
using Unity.Entities;

namespace Game.Workshop
{
    /// <summary>
    /// Обрабатывает установку ТИПА станции в слоте (из пустого -> установлен тип).
    /// Рецепт выбирается отдельно через SetStationRecipeRequest.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(WorkshopSystem))]
    public partial struct WorkshopStationInstallSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI
                .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (req, reqEntity) in SystemAPI.Query<RefRO<InstallStationTypeRequest>>().WithEntityAccess())
            {
                var r = req.ValueRO;
                if (!SystemAPI.Exists(r.Workshop) || !SystemAPI.HasBuffer<StationSlot>(r.Workshop))
                { ecb.DestroyEntity(reqEntity); continue; }

                ecb.AddComponent<WorkshopChainChangedTag>(r.Workshop); // Добавляем тег

                var slots = SystemAPI.GetBuffer<StationSlot>(r.Workshop);
                if (r.SlotIndex < 0 || r.SlotIndex >= slots.Length)
                { ecb.DestroyEntity(reqEntity); continue; }

                var stEnt = slots[r.SlotIndex].Station;
                if (!SystemAPI.Exists(stEnt))
                { ecb.DestroyEntity(reqEntity); continue; }

                // Устанавливаем тип станции
                if (SystemAPI.HasComponent<StationConfig>(stEnt))
                {
                    var cfg = SystemAPI.GetComponent<StationConfig>(stEnt);
                    cfg.StationTypeID = r.StationTypeID;
                    SystemAPI.SetComponent(stEnt, cfg);
                }

                // Сбрасываем состояние (рецепт ещё не выбран)
                if (SystemAPI.HasComponent<StationState>(stEnt))
                {
                    var st = SystemAPI.GetComponent<StationState>(stEnt);
                    st.SelectedRecipeID = -1;
                    st.RemainingTime = 0;
                    st.Status = StationStatus.Offline;
                    st.Enabled = 0;
                    SystemAPI.SetComponent(stEnt, st);
                }

                // Чистим накопленный выход
                if (SystemAPI.HasBuffer<StationOutputBufferElement>(stEnt))
                {
                    SystemAPI.GetBuffer<StationOutputBufferElement>(stEnt).Clear();
                }

                ecb.DestroyEntity(reqEntity);
            }
        }
    }
}