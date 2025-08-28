using Unity.Entities;
using UnityEngine;
using Wiring;
using Conveyor;

/// <summary>
/// Идемпотентная система, которая синхронизирует игровой режим с предметом в руках.
/// Если режим не соответствует предмету, создает ОДИН запрос на вход/выход из режима.
/// Не управляет состоянием напрямую, только создает запросы.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ActiveItemSystem))]
public partial class QuickbarItemUsageSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<GameState>();
        RequireForUpdate<PlayerTag>();
    }

    protected override void OnUpdate()
    {
        var gsEntity = SystemAPI.GetSingletonEntity<GameState>();
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

        if (SystemAPI.HasComponent<InUIMode>(gsEntity))
        {
            return;
        }

        var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
        var reg = ItemRegistry.Instance;
        if (reg == null) return;

        Item itemData = null;
        if (SystemAPI.HasComponent<ActiveEquippedItem>(playerEntity))
        {
            var equipped = SystemAPI.GetComponent<ActiveEquippedItem>(playerEntity);
            if (equipped.ItemID > 0)
            {
                itemData = reg.GetItemData(equipped.ItemID);
            }
        }

        bool isInBuildingMode = SystemAPI.HasComponent<InBuildingMode>(gsEntity);
        bool isInWireMode = SystemAPI.HasComponent<InWirePlacementMode>(gsEntity);
        bool isInConveyorMode = SystemAPI.HasComponent<InConveyorMode>(gsEntity);
        bool isInDefaultMode = !isInBuildingMode && !isInWireMode && !isInConveyorMode;

        if (itemData == null || (itemData.itemType != ItemType.Building && itemData.itemType != ItemType.Conveyor && !(itemData is WireItem)))
        {
            if (!isInDefaultMode)
            {
                if (isInBuildingMode)
                {
                    var req = ecb.CreateEntity();
                    ecb.AddComponent<ExitBuildingModeRequest>(req);
                }
                if (isInWireMode)
                {
                    var req = ecb.CreateEntity();
                    ecb.AddComponent<ExitWirePlacementModeRequest>(req);
                }
                if (isInConveyorMode)
                {
                    var req = ecb.CreateEntity();
                    ecb.AddComponent<ExitConveyorModeRequest>(req);
                }
            }
            return;
        }

        switch (itemData.itemType)
        {
            case ItemType.Building:
                bool needsToEnterBuilding = !isInBuildingMode ||
                                    (SystemAPI.HasComponent<BuildingState>(gsEntity) && SystemAPI.GetComponent<BuildingState>(gsEntity).BuildingItemID != itemData.itemID);

                if (needsToEnterBuilding)
                {
                    var req = ecb.CreateEntity();
                    ecb.AddComponent(req, new EnterBuildingModeRequest { ItemID = itemData.itemID });
                }
                break;

            case ItemType.Conveyor:
                bool needsToEnterConveyor = !isInConveyorMode ||
                                   (SystemAPI.HasComponent<ConveyorState>(gsEntity) && SystemAPI.GetComponent<ConveyorState>(gsEntity).ItemID != itemData.itemID);
                if (needsToEnterConveyor)
                {
                    var req = ecb.CreateEntity();
                    ecb.AddComponent(req, new EnterConveyorModeRequest { ItemID = itemData.itemID });
                }
                break;

            case ItemType.Miscellaneous when itemData is WireItem w:
                bool needsToEnterWire = !isInWireMode ||
                                   (SystemAPI.HasComponent<WireState>(gsEntity) && SystemAPI.GetComponent<WireState>(gsEntity).SelectedWireLevel != w.wireLevel);

                if (needsToEnterWire)
                {
                    var req = ecb.CreateEntity();
                    ecb.AddComponent(req, new EnterWirePlacementModeRequest { WireLevel = w.wireLevel });
                }
                break;
        }
    }
}