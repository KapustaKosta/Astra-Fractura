using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Проверяет, может ли NPC собирать ресурсы (есть ли ресурс рядом и место в инвентарях).
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
        var ecb = SystemAPI
            .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

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
        var ltwLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true); // Этот Lookup нам теперь нужен для AIPhysicsQuery

        // Фильтр для поиска ресурсов
        var resourceFilter = new CollisionFilter
        {
            BelongsTo = ~0u,
            CollidesWith = (uint)(1u << settings.ResourceCollisionLayer),
            GroupIndex = 0
        };

        // Теперь мы полностью полагаемся на AIPhysicsQuery.

        Entities
            .WithName("HarvestConditionSystem")
            .WithoutBurst()
            .WithReadOnly(settlementInventory)
            .WithReadOnly(resourceNodeLookup)
            .WithReadOnly(ltwLookup)
            .ForEach((Entity entity, in LocalToWorld npcTransform, in DynamicBuffer<InventoryItemElement> npcInventory) =>
            {
                bool canHarvest = true;
                var log = new System.Text.StringBuilder();
                //log.AppendLine($"<b>[DEBUG] Проверка условий сбора для NPC {entity.Index}</b>");
                    
                // Вызываем унифицированный метод поиска из AIPhysicsQuery
                Entity nearestResourceNode = AIPhysicsQuery.FindNearestResource(
                    npcTransform.Position,
                    settings.AISearchRadius,
                    in collisionWorld,
                    resourceFilter,
                    in resourceNodeLookup,
                    in ltwLookup 
                );

                if (nearestResourceNode == Entity.Null)
                {
                    canHarvest = false;
                    //log.AppendLine($"<color=red>ПРОВАЛ:</color> Не найдены ресурсы в радиусе {settings.AISearchRadius}.");
                    //log.AppendLine("Подсказка: проверьте биты категорий PhysicsShape у ресурсов и соответствие AISettings.ResourceCollisionLayer.");
                }
                else
                {
                    float foundDistance = math.distance(npcTransform.Position, ltwLookup[nearestResourceNode].Position);
                    //log.AppendLine($"<color=green>УСПЕХ:</color> Найден ресурс {nearestResourceNode} на расстоянии ~{foundDistance:0.0}.");

                    var resourceType = resourceNodeLookup[nearestResourceNode].resourceType;
                    //log.AppendLine($"--> Тип ресурса: {resourceType}");

                    if (!resourceItemMap.TryGetItemID(resourceType, out int itemIDToHarvest))
                    {
                        canHarvest = false;
                        //log.AppendLine($"<color=red>ПРОВАЛ:</color> Нет ItemID для ресурса {resourceType}.");
                    }
                    else
                    {
                        //log.AppendLine($"<color=green>УСПЕХ:</color> {resourceType} => ItemID {itemIDToHarvest}");
                        var itemData = itemRegistry.GetItemData(itemIDToHarvest);
                        if (itemData == null)
                        {
                            canHarvest = false;
                            //log.AppendLine($"<color=red>ПРОВАЛ:</color> Нет данных ItemRegistry для {itemIDToHarvest}.");
                        }
                        else
                        {
                            if (!InventoryUtils.HasSpaceForItem(npcInventory, itemIDToHarvest, itemData.maxStack))
                            {
                                canHarvest = false;
                                //log.AppendLine("<color=red>ПРОВАЛ:</color> Нет места в инвентаре NPC.");
                            }
                            else if (isSettlementInventoryFull && !InventoryUtils.HasSpaceForItem(settlementInventory, itemIDToHarvest, itemData.maxStack))
                            {
                                canHarvest = false;
                                //log.AppendLine("<color=red>ПРОВАЛ:</color> Нет места на складе поселения.");
                            }
                        }
                    }
                }

                //Debug.Log(log.ToString());

                bool hasTag = SystemAPI.HasComponent<HarvestingBlockedTag>(entity);
                if (!canHarvest && !hasTag) ecb.AddComponent<HarvestingBlockedTag>(entity);
                else if (canHarvest && hasTag) ecb.RemoveComponent<HarvestingBlockedTag>(entity);
            })
            .Run();
    }

}