using Unity.Entities;

/// <summary>
/// Система, отвечающая за определение намерения ИГРОКА начать добычу ресурсов.
/// Она проверяет условия, такие как нажатие кнопки действия, наличие цели-ресурса,
/// а также наличие в руках игрока подходящего инструмента.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(TargetDetectorSystem))]
[UpdateBefore(typeof(HarvestingSystem))]
public partial class PlayerHarvestIntentionSystem : SystemBase
{
    /// <summary>
    /// Вызывается каждый кадр для определения намерения добычи у игрока.
    /// </summary>
    protected override void OnUpdate()
    {
        var ecbSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
        var ecb = ecbSystem.CreateCommandBuffer();
        var itemRegistry = ItemRegistry.Instance;

        if (itemRegistry == null) return;
        
        Entities
            .WithoutBurst()
            // Запрос для игрока: ищем сущность с тегом игрока и активной целью.
            .ForEach((Entity playerEntity, in InputsData inputs) =>
            {
                bool alreadyWantsToHarvest = SystemAPI.HasComponent<WantsToHarvestTag>(playerEntity);

                // Добавляем намерение, если нажата кнопка, цель - ресурс, и намерения еще нет.
                if (!inputs.PrimaryAction)
                {
                    if (alreadyWantsToHarvest)
                    {
                        ecb.RemoveComponent<WantsToHarvestTag>(playerEntity);
                    }
                    return;
                }
                
                if (!SystemAPI.HasComponent<ActiveTarget>(playerEntity))
                {
                    if (alreadyWantsToHarvest) ecb.RemoveComponent<WantsToHarvestTag>(playerEntity);
                    return;
                }
                
                var targetEntity = SystemAPI.GetComponent<ActiveTarget>(playerEntity).Value;
                if (!SystemAPI.HasComponent<ResourceNode>(targetEntity))
                {
                    if (alreadyWantsToHarvest) ecb.RemoveComponent<WantsToHarvestTag>(playerEntity);
                    return;
                }
                
                bool hasValidTool = false;
                if (SystemAPI.HasComponent<ActiveEquippedItem>(playerEntity))
                {
                    var equippedItemID = SystemAPI.GetComponent<ActiveEquippedItem>(playerEntity).ItemID;
                    var equippedItemData = itemRegistry.GetItemData(equippedItemID);
                    
                    if (equippedItemData != null && equippedItemData.itemType == ItemType.Tool)
                    {
                        var resourceNode = SystemAPI.GetComponent<ResourceNode>(targetEntity);
                        
                        // Приводим enum к числовому значению для побитовой проверки флага.
                        // ResourceCollectionType.Wood (0) -> 1 << 0 -> флаг 1 (Wood)
                        // ResourceCollectionType.Stone (1) -> 1 << 1 -> флаг 2 (Stone) и т.д.
                        var resourceFlag = (ResourceType)(1 << (int)resourceNode.resourceType);
                        
                        if (equippedItemData.canHarvest.HasFlag(resourceFlag))
                        {
                            hasValidTool = true;
                        }
                    }
                }
                
                if (inputs.PrimaryAction && hasValidTool && !alreadyWantsToHarvest)
                {
                    ecb.AddComponent<WantsToHarvestTag>(playerEntity);
                }
                else if (!hasValidTool && alreadyWantsToHarvest)
                {
                     ecb.RemoveComponent<WantsToHarvestTag>(playerEntity);
                }
                
            }).Run();
    }
}