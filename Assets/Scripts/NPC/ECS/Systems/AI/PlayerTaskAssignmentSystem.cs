using Unity.Entities;
using Unity.Collections;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class PlayerTaskAssignmentSystem : SystemBase
{
    private EntityQuery m_RequestQuery;

    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<AISettings>();
        RequireForUpdate<PlayerSettlementTag>();
        m_RequestQuery = GetEntityQuery(typeof(PlayerAssignHarvestRequest));
    }
    
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        var settings = SystemAPI.GetSingleton<AISettings>();
        
        var resourceItemMap = ResourceItemMapping.Instance;
        var itemRegistry = ItemRegistry.Instance;
        if (resourceItemMap == null || itemRegistry == null) return;
        
        var settlementEntity = SystemAPI.GetSingletonEntity<PlayerSettlementTag>();
        bool isSettlementInventoryFull = SystemAPI.HasComponent<SettlementInventoryFullTag>(settlementEntity);
        
        foreach (var entity in m_RequestQuery.ToEntityArray(Allocator.Temp))
        {
            var request = EntityManager.GetComponentData<PlayerAssignHarvestRequest>(entity);

            if (!SystemAPI.HasComponent<NPCBrain>(request.TargetNPC) || 
                !SystemAPI.HasComponent<ResourceNode>(request.TargetResourceNode))
            {
                ecb.DestroyEntity(entity);
                continue;
            }

            var resourceNodeData = SystemAPI.GetComponent<ResourceNode>(request.TargetResourceNode);
            if (!resourceItemMap.TryGetItemID(resourceNodeData.resourceType, out int itemIDToHarvest) || itemIDToHarvest == 0)
            {
                ecb.DestroyEntity(entity);
                continue;
            }
            
            var inventory = SystemAPI.GetBuffer<InventoryItemElement>(request.TargetNPC);
            var itemData = itemRegistry.GetItemData(itemIDToHarvest);
            
            bool npcHasSpace = itemData != null && InventoryUtils.HasSpaceForItem(inventory, itemIDToHarvest, itemData.maxStack);

            if (SystemAPI.HasComponent<ActiveGoal>(request.TargetNPC))
            {
                var oldGoal = SystemAPI.GetComponent<ActiveGoal>(request.TargetNPC);
                ecb.AddComponent(request.TargetNPC, new CleanupGoalRequest { OldGoalType = oldGoal.Type });
            }
            
            if (npcHasSpace && !isSettlementInventoryFull)
            {
                var newGoal = new ActiveGoal
                {
                    Type = GoalType.Harvest,
                    Target = request.TargetResourceNode,
                    CurrentGoalScore = settings.PlayerAssignHarvestPriority,
                    RelevantItemID = itemIDToHarvest
                };
                
                if (SystemAPI.HasComponent<ActiveGoal>(request.TargetNPC)) 
                    ecb.SetComponent(request.TargetNPC, newGoal);
                else 
                    ecb.AddComponent(request.TargetNPC, newGoal);
            }
            else if (!npcHasSpace && !isSettlementInventoryFull)
            {
                // ИСПОЛЬЗУЕМ НОВУЮ УТИЛИТУ
                int firstItemID = InventoryUtils.GetFirstItemID(inventory);

                var returnGoal = new ActiveGoal
                {
                    Type = GoalType.ReturnToBase,
                    Target = settlementEntity,
                    CurrentGoalScore = settings.PlayerAssignReturnPriority,
                    RelevantItemID = firstItemID
                };
                
                if (SystemAPI.HasComponent<ActiveGoal>(request.TargetNPC)) 
                    ecb.SetComponent(request.TargetNPC, returnGoal);
                else 
                    ecb.AddComponent(request.TargetNPC, returnGoal);
                
                CreateUINotification(ecb, "Мой инвентарь полон! Сначала разгружусь, потом выполню приказ.");
            }
            else if (isSettlementInventoryFull)
            {
                if (!npcHasSpace)
                    CreateUINotification(ecb, "Невозможно выполнить приказ! Мой инвентарь и склад на базе заполнены.");
                else
                    CreateUINotification(ecb, "Не могу добывать: склад на базе заполнен! Приказ отменен.");
            }
            
            ecb.DestroyEntity(entity);
        }
    }

    private void CreateUINotification(EntityCommandBuffer ecb, string message)
    {
        var notificationEntity = ecb.CreateEntity();
        ecb.AddComponent(notificationEntity, new UINotificationRequest { Message = message });
    }
}