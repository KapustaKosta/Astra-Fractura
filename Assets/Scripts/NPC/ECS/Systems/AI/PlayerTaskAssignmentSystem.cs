using Unity.Entities;
using Unity.Collections;
using UnityEngine;

/// <summary>
/// Система назначения задач игроком NPC.
/// Обрабатывает запросы на сбор ресурсов от игрока и назначает соответствующие цели NPC.
/// Учитывает доступность ресурса, наличие места в инвентаре NPC и вместимость склада базы.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class PlayerTaskAssignmentSystem : SystemBase
{
    private EntityQuery m_RequestQuery;

    protected override void OnCreate()
    {
        // Инициализируем систему и создаем запрос для обработки запросов игрока
        base.OnCreate();
        RequireForUpdate<AISettings>();
        RequireForUpdate<PlayerSettlementTag>();
        m_RequestQuery = GetEntityQuery(typeof(PlayerAssignHarvestRequest));
    }
    
    protected override void OnUpdate()
    {
        // Получаем буфер команд для изменения сущностей
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);
        
        // Получаем глобальные настройки ИИ
        var settings = SystemAPI.GetSingleton<AISettings>();
        
        // Получаем доступ к картам ресурсов и реестру предметов
        var resourceItemMap = ResourceItemMapping.Instance;
        var itemRegistry = ItemRegistry.Instance;
        if (resourceItemMap == null || itemRegistry == null) return;
        
        // Получаем сущность базы игрока
        var settlementEntity = SystemAPI.GetSingletonEntity<PlayerSettlementTag>();
        // Проверяем, заполнен ли инвентарь базы
        bool isSettlementInventoryFull = SystemAPI.HasComponent<SettlementInventoryFullTag>(settlementEntity);
        
        // Обрабатываем все запросы на назначение задач
        foreach (var entity in m_RequestQuery.ToEntityArray(Allocator.Temp))
        {
            // Получаем данные запроса
            var request = EntityManager.GetComponentData<PlayerAssignHarvestRequest>(entity);

            // Проверяем валидность целевых сущностей
            if (!SystemAPI.HasComponent<NPCBrain>(request.TargetNPC) || 
                !SystemAPI.HasComponent<ResourceNode>(request.TargetResourceNode))
            {
                ecb.DestroyEntity(entity);
                continue;
            }

            // Получаем данные о ресурсе
            var resourceNodeData = SystemAPI.GetComponent<ResourceNode>(request.TargetResourceNode);
            if (!resourceItemMap.TryGetItemID(resourceNodeData.resourceType, out int itemIDToHarvest) || itemIDToHarvest == 0)
            {
                ecb.DestroyEntity(entity);
                continue;
            }
            
            // Получаем доступ к инвентарю NPC
            var inventory = SystemAPI.GetBuffer<InventoryItemElement>(request.TargetNPC);
            // Получаем данные о предмете
            var itemData = itemRegistry.GetItemData(itemIDToHarvest);
            
            // Проверяем, есть ли место в инвентаре NPC
            bool npcHasSpace = itemData != null && InventoryUtils.HasSpaceForItem(inventory, itemIDToHarvest, itemData.maxStack);

            // Очищаем текущую цель NPC, если она существует
            if (SystemAPI.HasComponent<ActiveGoal>(request.TargetNPC))
            {
                var oldGoal = SystemAPI.GetComponent<ActiveGoal>(request.TargetNPC);
                ecb.AddComponent(request.TargetNPC, new CleanupGoalRequest { OldGoalType = oldGoal.Type });
            }
            
            // Логика назначения целей:
            if (npcHasSpace && !isSettlementInventoryFull)
            {
                // Устанавливаем цель сбора ресурса
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
                // Получаем ID первого предмета в инвентаре
                int firstItemID = InventoryUtils.GetFirstItemID(inventory);

                // Устанавливаем цель возврата к базе
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
                
                // Создаем уведомление для игрока
                CreateUINotification(ecb, "Мой инвентарь полон! Сначала разгружусь, потом выполню приказ.");
            }
            else if (isSettlementInventoryFull)
            {
                // Обработка ситуаций с переполненным складом
                if (!npcHasSpace)
                    CreateUINotification(ecb, "Невозможно выполнить приказ! Мой инвентарь и склад на базе заполнены.");
                else
                    CreateUINotification(ecb, "Не могу добывать: склад на базе заполнен! Приказ отменен.");
            }
            
            // Удаляем обработанный запрос
            ecb.DestroyEntity(entity);
        }
    }

    /// <summary>
    /// Создает уведомление в интерфейсе игрока.
    /// </summary>
    /// <param name="ecb">Буфер команд для создания сущности уведомления</param>
    /// <param name="message">Текст сообщения для отображения</param>
    private void CreateUINotification(EntityCommandBuffer ecb, string message)
    {
        var notificationEntity = ecb.CreateEntity();
        ecb.AddComponent(notificationEntity, new UINotificationRequest { Message = message });
    }
}