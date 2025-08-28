using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Wiring
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class EnterWirePlacementModeSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<GameState>();
        }

        protected override void OnUpdate()
        {
            var reqQuery = SystemAPI.QueryBuilder().WithAll<EnterWirePlacementModeRequest>().Build();
            if (reqQuery.IsEmpty)
            {
                return;
            }

            // Используем надежный EndSimulation ECB
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(World.Unmanaged);

            var gameStateEntity = SystemAPI.GetSingletonEntity<GameState>();

            Debug.Log("<color=orange>[EnterWirePlacementModeSystem] Обнаружены запросы на вход в режим проводов.</color>");

            foreach (var (req, reqEntity) in SystemAPI.Query<RefRO<EnterWirePlacementModeRequest>>().WithEntityAccess())
            {
                if (SystemAPI.HasComponent<InWirePlacementMode>(gameStateEntity))
                {
                    ecb.DestroyEntity(reqEntity);
                    continue;
                }

                if (SystemAPI.HasComponent<InBuildingMode>(gameStateEntity))
                {
                    ecb.RemoveComponent<InBuildingMode>(gameStateEntity);
                    if (SystemAPI.HasComponent<BuildingState>(gameStateEntity))
                        ecb.RemoveComponent<BuildingState>(gameStateEntity);
                }
                if (SystemAPI.HasComponent<InUIMode>(gameStateEntity))
                {
                    ecb.RemoveComponent<InUIMode>(gameStateEntity);
                    if (SystemAPI.HasComponent<UIState>(gameStateEntity))
                        ecb.RemoveComponent<UIState>(gameStateEntity);
                }
                if (SystemAPI.HasComponent<InDefaultMode>(gameStateEntity))
                {
                    ecb.RemoveComponent<InDefaultMode>(gameStateEntity);
                }

                Debug.Log($"<color=lime>[EnterWirePlacementModeSystem] Переключаю GameState на InWirePlacementMode (уровень {req.ValueRO.WireLevel}).</color>");

                ecb.AddComponent<InWirePlacementMode>(gameStateEntity);
                ecb.AddComponent(gameStateEntity, new WireState
                {
                    SelectedWireLevel = math.clamp(req.ValueRO.WireLevel, 1, 3),
                    StartConnector = Entity.Null,
                    WirePreviewEntity = Entity.Null
                });

                ecb.DestroyEntity(reqEntity);
            }
        }
    }
}