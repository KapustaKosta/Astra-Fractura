using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;

/// <summary>
/// Система, определяющая, может ли NPC собирать ресурсы, проверяя доступность ресурсов и место в инвентаре.
/// Обновляется в группе SimulationSystemGroup перед NPCTaskArbiterSystem.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(NPCTaskArbiterSystem))]
public partial class HarvestConditionSystem : SystemBase
{
    /// <summary>
    /// Проверяет наличие необходимых компонентов перед запуском системы.
    /// Требует наличия AISettings, PlayerSettlementTag и PhysicsWorldSingleton.
    /// </summary>
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<AISettings>();
        RequireForUpdate<PlayerSettlementTag>();
        RequireForUpdate<PhysicsWorldSingleton>();
    }

    /// <summary>
    /// Основной метод системы, выполняющий проверку условий сбора ресурсов для NPC.
    /// Создает командный буфер, получает данные о ресурсах и проверяет инвентари.
    /// </summary>
    protected override void OnUpdate()
    {
        // Получаем буфер команд для изменения сущностей
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        
        // Получаем доступ к глобальным данным
        var resourceItemMap = ResourceItemMapping.Instance;
        var itemRegistry = ItemRegistry.Instance;
        if (resourceItemMap == null || itemRegistry == null) return;
        
        // Получаем данные о поселении
        var settlementEntity = SystemAPI.GetSingletonEntity<PlayerSettlementTag>();
        bool isSettlementInventoryFull = SystemAPI.HasComponent<SettlementInventoryFullTag>(settlementEntity);
        var settlementInventory = SystemAPI.GetBuffer<InventoryItemElement>(settlementEntity);
        
        // Получаем настройки и физический мир
        var settings = SystemAPI.GetSingleton<AISettings>();
        var collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
        var resourceNodeLookup = SystemAPI.GetComponentLookup<ResourceNode>(true);

        // Фильтр для поиска ресурсов
        var resourceFilter = new CollisionFilter
        {
            BelongsTo = ~0u,
            CollidesWith = (uint)(1 << settings.ResourceCollisionLayer),
            GroupIndex = 0
        };

        // Итерируемся по NPC с инвентарем и наймом
        Entities
            .WithoutBurst()
            .WithAll<NPCBrain, HasInventoryTag, NPCHiredTag>()
            .WithReadOnly(settlementInventory)
            .WithReadOnly(resourceNodeLookup)
            .ForEach((Entity entity, in LocalToWorld npcTransform, in DynamicBuffer<InventoryItemElement> npcInventory) =>
            {
                bool canHarvest = true;
                string blockReason = "";
                int itemIDToHarvest = 0; // Идентификатор ресурса для сбора
                Item itemData = null; // Данные ресурса

                // 1. Поиск ближайшего ресурсного узла
                Entity nearestResourceNode = AIPhysicsQuery.FindNearestResource(
                    npcTransform.Position,
                    settings.AISearchRadius,
                    in collisionWorld,
                    resourceFilter,
                    in resourceNodeLookup
                );

                if (nearestResourceNode == Entity.Null)
                {
                    canHarvest = false;
                    blockReason = $"Не найдены ресурсы в радиусе {settings.AISearchRadius}";
                }

                // 2. Проверка маппинга ресурса в предмет
                if (canHarvest)
                {
                    var resourceType = SystemAPI.GetComponent<ResourceNode>(nearestResourceNode).resourceType;
                    if (!resourceItemMap.TryGetItemID(resourceType, out itemIDToHarvest))
                    {
                        canHarvest = false;
                        blockReason = $"Не удалось найти ItemID для ресурса {resourceType}";
                    }
                    else if ((itemData = itemRegistry.GetItemData(itemIDToHarvest)) == null)
                    {
                        canHarvest = false;
                        blockReason = $"Не найдены данные для предмета {itemIDToHarvest} в ItemRegistry";
                    }
                    else
                    {
                        // 3. Проверка места в инвентаре NPC
                        if (!InventoryUtils.HasSpaceForItem(npcInventory, itemIDToHarvest, itemData.maxStack))
                        {
                            canHarvest = false;
                            blockReason = "В инвентаре NPC нет места для данного ресурса";
                        }
                        else
                        {
                            // 4. Проверка места на складе поселения
                            bool settlementHasSpace = !isSettlementInventoryFull || 
                                InventoryUtils.HasSpaceForItem(settlementInventory, itemIDToHarvest, itemData.maxStack);
                            
                            if (!settlementHasSpace)
                            {
                                canHarvest = false;
                                blockReason = "На складе поселения нет места для данного ресурса";
                            }
                        }
                    }
                }
                
                // Обновляем статус блокировки сбора
                bool hasTag = SystemAPI.HasComponent<HarvestingBlockedTag>(entity);
                if (!canHarvest && !hasTag)
                {
                    ecb.AddComponent<HarvestingBlockedTag>(entity);
                }
                else if (canHarvest && hasTag)
                {
                    ecb.RemoveComponent<HarvestingBlockedTag>(entity);
                }
            }).Run();
    }
}