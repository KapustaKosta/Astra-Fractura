using Energy.Core;
using Game.Production;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Wiring;
using Unity.Collections;
using Conveyor;
using Game.Workshop;
using Game.Research;

#region Gameplay Mode Transitions

[UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
public partial class EnsureDefaultModeOnStartSystem : SystemBase
{
    private bool _done;

    protected override void OnCreate()
    {
        RequireForUpdate<GameState>();
    }

    protected override void OnUpdate()
    {
        if (_done || !SystemAPI.TryGetSingletonEntity<GameState>(out var gs))
            return;

        bool hasAnyMode =
            SystemAPI.HasComponent<InDefaultMode>(gs) ||
            SystemAPI.HasComponent<InBuildingMode>(gs) ||
            SystemAPI.HasComponent<InWirePlacementMode>(gs) ||
            SystemAPI.HasComponent<InConveyorMode>(gs) ||
            SystemAPI.HasComponent<InUIMode>(gs);

        if (!hasAnyMode)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            ecb.AddComponent<InDefaultMode>(gs);
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        _done = true;
    }
}

[UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
public partial class EnterBuildingModeSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<GameState>();
    }

    protected override void OnUpdate()
    {
        var reqQuery = SystemAPI.QueryBuilder().WithAll<EnterBuildingModeRequest>().Build();
        if (reqQuery.IsEmpty) return;

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        if (!SystemAPI.TryGetSingletonEntity<GameState>(out var gameState)) return;

        foreach (var (_, entity) in SystemAPI.Query<RefRO<BuildingPreviewTag>>().WithEntityAccess())
        {
            ecb.DestroyEntity(entity);
        }

        var req = SystemAPI.GetSingleton<EnterBuildingModeRequest>();

        if (SystemAPI.TryGetSingletonEntity<ResearchState>(out var researchEntity) &&
            EntityManager.HasBuffer<UnlockedResearchItem>(researchEntity))
        {
            var unlockedItems = EntityManager.GetBuffer<UnlockedResearchItem>(researchEntity);
            if (unlockedItems.Length > 0 && !IsItemUnlocked(unlockedItems, req.ItemID))
            {
                var notification = ecb.CreateEntity();
                ecb.AddComponent(notification, new UINotificationRequest
                {
                    Message = new FixedString128Bytes("Technology is locked")
                });

                ecb.DestroyEntity(reqQuery, EntityQueryCaptureMode.AtPlayback);
                return;
            }
        }
        StateTransitionHelpers.CleanupModes(EntityManager, ecb, gameState);
        ecb.AddComponent<InBuildingMode>(gameState);

        var prefab = ItemToEntityResolver.GetEntityPrefabFromID(EntityManager, req.ItemID);
        if (prefab != Entity.Null)
        {
            ecb.AddComponent(gameState, new BuildingState { BuildingPrefabToPlace = prefab, BuildingItemID = req.ItemID });
        }
        else
        {
            Debug.LogError($"[EnterBuildingModeSystem] Не удалось найти префаб для ItemID: {req.ItemID}.");
        }

        ecb.DestroyEntity(reqQuery, EntityQueryCaptureMode.AtPlayback);
    }

    private static bool IsItemUnlocked(DynamicBuffer<UnlockedResearchItem> buffer, int itemId)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            if (buffer[i].ItemId == itemId)
            {
                return true;
            }
        }

        return false;
    }
}
[UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
public partial class ExitBuildingModeSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<GameState>();
    }

    protected override void OnUpdate()
    {
        var reqQuery = SystemAPI.QueryBuilder().WithAll<ExitBuildingModeRequest>().Build();
        if (reqQuery.IsEmpty) return;

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        if (!SystemAPI.TryGetSingletonEntity<GameState>(out var gameState)) return;

        if (SystemAPI.HasComponent<InBuildingMode>(gameState))
        {
            foreach (var (_, entity) in SystemAPI.Query<RefRO<BuildingPreviewTag>>().WithEntityAccess())
            {
                ecb.DestroyEntity(entity);
            }

            StateTransitionHelpers.CleanupModes(EntityManager, ecb, gameState);
            ecb.AddComponent<InDefaultMode>(gameState);
        }

        ecb.DestroyEntity(reqQuery, EntityQueryCaptureMode.AtPlayback);
    }
}

[UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
public partial class EnterWirePlacementModeSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<GameState>();
    }

    protected override void OnUpdate()
    {
        var reqQuery = SystemAPI.QueryBuilder().WithAll<EnterWirePlacementModeRequest>().Build();
        if (reqQuery.IsEmpty) return;

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        if (!SystemAPI.TryGetSingletonEntity<GameState>(out var gameState)) return;

        var req = SystemAPI.GetSingleton<EnterWirePlacementModeRequest>();
        StateTransitionHelpers.CleanupModes(EntityManager, ecb, gameState);
        ecb.AddComponent<InWirePlacementMode>(gameState);
        ecb.AddComponent(gameState, new WireState
        {
            SelectedWireLevel = math.clamp(req.WireLevel, 1, 3),
            StartConnector = Entity.Null,
            WirePreviewEntity = Entity.Null
        });

        ecb.DestroyEntity(reqQuery, EntityQueryCaptureMode.AtPlayback);
    }
}

[UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
public partial class ExitWirePlacementModeSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<GameState>();
    }

    protected override void OnUpdate()
    {
        var reqQuery = SystemAPI.QueryBuilder().WithAll<ExitWirePlacementModeRequest>().Build();
        if (reqQuery.IsEmpty) return;

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        if (!SystemAPI.TryGetSingletonEntity<GameState>(out var gameState)) return;

        if (SystemAPI.HasComponent<InWirePlacementMode>(gameState))
        {
            var previewsQ = GetEntityQuery(ComponentType.ReadOnly<WirePreviewTag>());
            var pendingQ = GetEntityQuery(ComponentType.ReadOnly<PendingWire>());
            ecb.DestroyEntity(previewsQ, EntityQueryCaptureMode.AtPlayback);
            ecb.DestroyEntity(pendingQ, EntityQueryCaptureMode.AtPlayback);

            StateTransitionHelpers.CleanupModes(EntityManager, ecb, gameState);
            ecb.AddComponent<InDefaultMode>(gameState);
        }

        ecb.DestroyEntity(reqQuery, EntityQueryCaptureMode.AtPlayback);
    }
}

[UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
public partial class EnterConveyorModeSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<GameState>();
    }

    protected override void OnUpdate()
    {
        var reqQuery = SystemAPI.QueryBuilder().WithAll<EnterConveyorModeRequest>().Build();
        if (reqQuery.IsEmpty) return;

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        if (!SystemAPI.TryGetSingletonEntity<GameState>(out var gameState)) return;

        var req = SystemAPI.GetSingleton<EnterConveyorModeRequest>();
        StateTransitionHelpers.CleanupModes(EntityManager, ecb, gameState);
        ecb.AddComponent<InConveyorMode>(gameState);
        ecb.AddComponent(gameState, new ConveyorState
        {
            ItemID = req.ItemID,
            PreviewEntity = Entity.Null,
            StartConnector = Entity.Null,
            HasStart = false,
            SnapRadius = 2.0f
        });

        ecb.DestroyEntity(reqQuery, EntityQueryCaptureMode.AtPlayback);
    }
}

[UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
public partial class ExitConveyorModeSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<GameState>();
    }

    protected override void OnUpdate()
    {
        var reqQuery = SystemAPI.QueryBuilder().WithAll<ExitConveyorModeRequest>().Build();
        if (reqQuery.IsEmpty) return;

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        if (!SystemAPI.TryGetSingletonEntity<GameState>(out var gameState)) return;

        if (SystemAPI.HasComponent<InConveyorMode>(gameState))
        {
            StateTransitionHelpers.CleanupModes(EntityManager, ecb, gameState);
            ecb.AddComponent<InDefaultMode>(gameState);
        }

        ecb.DestroyEntity(reqQuery, EntityQueryCaptureMode.AtPlayback);
    }
}

#endregion

#region UI Mode Transitions


[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BeginSimulationEntityCommandBufferSystem))]
public partial class ToggleInventorySystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<GameState>();
    }

    protected override void OnUpdate()
    {
        var inventoryRequestQuery = SystemAPI.QueryBuilder().WithAll<ToggleInventoryRequest>().Build();
        var productionRequestQuery = SystemAPI.QueryBuilder().WithAll<OpenProductionUIRequest>().Build();
        var workshopRequestQuery = SystemAPI.QueryBuilder().WithAll<OpenWorkshopUIRequest>().Build();
        var npcRequestQuery = SystemAPI.QueryBuilder().WithAll<OpenNPCUIRequest>().Build();
        var settlementRequestQuery = SystemAPI.QueryBuilder().WithAll<OpenSettlementUIRequest>().Build();
        var tradeRequestQuery = SystemAPI.QueryBuilder().WithAll<OpenTradeUIRequest>().Build();
        var generatorRequestQuery = SystemAPI.QueryBuilder().WithAll<OpenGeneratorUIRequest>().Build();
        var batteryRequestQuery = SystemAPI.QueryBuilder().WithAll<OpenBatteryUIRequest>().Build();
        var conveyorRoutesRequestQuery = SystemAPI.QueryBuilder().WithAll<OpenConveyorRoutesUIRequest>().Build();
        var researchRequestQuery = SystemAPI.QueryBuilder().WithAll<ToggleResearchUIRequest>().Build();

        bool hasAnyOpenRequest =
            !inventoryRequestQuery.IsEmpty ||
            !productionRequestQuery.IsEmpty ||
            !workshopRequestQuery.IsEmpty ||
            !npcRequestQuery.IsEmpty ||
            !settlementRequestQuery.IsEmpty ||
            !tradeRequestQuery.IsEmpty ||
            !generatorRequestQuery.IsEmpty ||
            !batteryRequestQuery.IsEmpty ||
            !conveyorRoutesRequestQuery.IsEmpty ||
            !researchRequestQuery.IsEmpty;

        if (!hasAnyOpenRequest)
        {
            return;
        }

        var ecb = SystemAPI
            .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

        if (!SystemAPI.TryGetSingletonEntity<GameState>(out var gameState))
        {
            return;
        }

        bool isInUIMode = SystemAPI.HasComponent<InUIMode>(gameState);
        bool hasUIState = SystemAPI.HasComponent<UIState>(gameState);
        UIType currentUIType = hasUIState ? SystemAPI.GetComponent<UIState>(gameState).ActiveUIType : UIType.None;

        if (!inventoryRequestQuery.IsEmpty && isInUIMode)
        {
            var closeRequest = ecb.CreateEntity();
            ecb.AddComponent<CloseAllUIRequest>(closeRequest);
            ecb.DestroyEntity(inventoryRequestQuery, EntityQueryCaptureMode.AtPlayback);
            return;
        }

        if (!researchRequestQuery.IsEmpty && isInUIMode && currentUIType == UIType.Research)
        {
            var closeRequest = ecb.CreateEntity();
            ecb.AddComponent<CloseAllUIRequest>(closeRequest);
            ecb.DestroyEntity(researchRequestQuery, EntityQueryCaptureMode.AtPlayback);
            return;
        }

        StateTransitionHelpers.CleanupModes(EntityManager, ecb, gameState);
        ecb.AddComponent<InUIMode>(gameState);

        if (!inventoryRequestQuery.IsEmpty)
        {
            ecb.AddComponent(gameState, new UIState { ActiveUIType = UIType.Inventory, ActiveUITarget = Entity.Null });
            ecb.DestroyEntity(inventoryRequestQuery, EntityQueryCaptureMode.AtPlayback);
        }
        else if (!productionRequestQuery.IsEmpty)
        {
            var req = SystemAPI.GetSingleton<OpenProductionUIRequest>();
            ecb.AddComponent(gameState, new UIState { ActiveUIType = UIType.Production, ActiveUITarget = req.Target });
            ecb.DestroyEntity(productionRequestQuery, EntityQueryCaptureMode.AtPlayback);
        }
        else if (!workshopRequestQuery.IsEmpty)
        {
            Debug.Log("<color=cyan>[State System]</color> Обнаружен OpenWorkshopUIRequest. Открываю UI цеха.");
            var req = SystemAPI.GetSingleton<OpenWorkshopUIRequest>();
            ecb.AddComponent(gameState, new UIState { ActiveUIType = UIType.Workshop, ActiveUITarget = req.Target });
            ecb.DestroyEntity(workshopRequestQuery, EntityQueryCaptureMode.AtPlayback);
        }
        else if (!conveyorRoutesRequestQuery.IsEmpty)
        {
            ecb.AddComponent(gameState, new UIState { ActiveUIType = UIType.ConveyorRoutes, ActiveUITarget = Entity.Null });
            ecb.DestroyEntity(conveyorRoutesRequestQuery, EntityQueryCaptureMode.AtPlayback);
        }
        else if (!npcRequestQuery.IsEmpty)
        {
            var req = SystemAPI.GetSingleton<OpenNPCUIRequest>();
            ecb.AddComponent(gameState, new UIState { ActiveUIType = UIType.NPC, ActiveUITarget = req.Target });
            ecb.DestroyEntity(npcRequestQuery, EntityQueryCaptureMode.AtPlayback);
        }
        else if (!settlementRequestQuery.IsEmpty)
        {
            var req = SystemAPI.GetSingleton<OpenSettlementUIRequest>();
            ecb.AddComponent(gameState, new UIState { ActiveUIType = UIType.Settlement, ActiveUITarget = req.Target });
            ecb.DestroyEntity(settlementRequestQuery, EntityQueryCaptureMode.AtPlayback);
        }
        else if (!tradeRequestQuery.IsEmpty)
        {
            var req = SystemAPI.GetSingleton<OpenTradeUIRequest>();
            ecb.AddComponent(gameState, new UIState { ActiveUIType = UIType.Trade, ActiveUITarget = req.Target });
            ecb.DestroyEntity(tradeRequestQuery, EntityQueryCaptureMode.AtPlayback);
        }
        else if (!generatorRequestQuery.IsEmpty)
        {
            var req = SystemAPI.GetSingleton<OpenGeneratorUIRequest>();
            ecb.AddComponent(gameState, new UIState { ActiveUIType = UIType.Generator, ActiveUITarget = req.Target });
            ecb.DestroyEntity(generatorRequestQuery, EntityQueryCaptureMode.AtPlayback);
        }
        else if (!batteryRequestQuery.IsEmpty)
        {
            var req = SystemAPI.GetSingleton<OpenBatteryUIRequest>();
            ecb.AddComponent(gameState, new UIState { ActiveUIType = UIType.Battery, ActiveUITarget = req.Target });
            ecb.DestroyEntity(batteryRequestQuery, EntityQueryCaptureMode.AtPlayback);
        }
        else if (!researchRequestQuery.IsEmpty)
        {
            ecb.AddComponent(gameState, new UIState { ActiveUIType = UIType.Research, ActiveUITarget = Entity.Null });
            ecb.DestroyEntity(researchRequestQuery, EntityQueryCaptureMode.AtPlayback);
        }
    }
}


[UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
public partial class CloseUISystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<GameState>();
    }

    protected override void OnUpdate()
    {
        var reqQuery = SystemAPI.QueryBuilder().WithAll<CloseAllUIRequest>().Build();
        if (reqQuery.IsEmpty) return;

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        if (!SystemAPI.TryGetSingletonEntity<GameState>(out var gameState)) return;

        if (SystemAPI.HasComponent<InUIMode>(gameState))
        {
            StateTransitionHelpers.CleanupModes(EntityManager, ecb, gameState);
            ecb.AddComponent<InDefaultMode>(gameState);
        }

        ecb.DestroyEntity(reqQuery, EntityQueryCaptureMode.AtPlayback);
    }
}

#endregion

public static class StateTransitionHelpers
{
    public static void CleanupModes(EntityManager entityManager, EntityCommandBuffer ecb, Entity gameState)
    {
        if (entityManager.HasComponent<InDefaultMode>(gameState)) ecb.RemoveComponent<InDefaultMode>(gameState);
        if (entityManager.HasComponent<InBuildingMode>(gameState)) ecb.RemoveComponent<InBuildingMode>(gameState);
        if (entityManager.HasComponent<InWirePlacementMode>(gameState)) ecb.RemoveComponent<InWirePlacementMode>(gameState);
        if (entityManager.HasComponent<InConveyorMode>(gameState)) ecb.RemoveComponent<InConveyorMode>(gameState);
        if (entityManager.HasComponent<InUIMode>(gameState)) ecb.RemoveComponent<InUIMode>(gameState);

        if (entityManager.HasComponent<BuildingState>(gameState)) ecb.RemoveComponent<BuildingState>(gameState);
        if (entityManager.HasComponent<WireState>(gameState)) ecb.RemoveComponent<WireState>(gameState);
        if (entityManager.HasComponent<ConveyorState>(gameState)) ecb.RemoveComponent<ConveyorState>(gameState);
        if (entityManager.HasComponent<UIState>(gameState)) ecb.RemoveComponent<UIState>(gameState);
    }
}