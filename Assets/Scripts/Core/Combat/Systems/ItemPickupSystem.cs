using Unity.Entities;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(PlayerContextualInteractionSystem))]
public partial class ItemPickupSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        var itemRegistry = ItemRegistry.Instance;
        if (itemRegistry == null) return;
        
        // Добавляем .WithoutBurst(), чтобы разрешить использование 'itemRegistry' (класса) внутри ForEach.
        Entities
            .WithoutBurst() 
            .ForEach((Entity requestEntity, in PickupRequest request) =>
            {
                // Теперь эта строка не вызовет ошибку компиляции.
                if (!SystemAPI.HasComponent<WorldItem>(request.LogicalItemEntity)) return;
                
                var worldItem = SystemAPI.GetComponent<WorldItem>(request.LogicalItemEntity);
                var itemData = itemRegistry.GetItemData(worldItem.ItemID);

                var playerInventory = SystemAPI.GetBuffer<InventoryItemElement>(request.Player);
                if (InventoryUtils.HasSpaceForItem(playerInventory, worldItem.ItemID, itemData.maxStack))
                {
                    var addItemReq = ecb.CreateEntity();
                    ecb.AddComponent(addItemReq, new AddItemRequest
                    {
                        TargetInventoryOwner = request.Player,
                        ItemID = worldItem.ItemID,
                        Amount = worldItem.Count
                    });

                    if (SystemAPI.HasComponent<LogicalItemHasVisual>(request.LogicalItemEntity))
                    {
                        var visualEntity = SystemAPI.GetComponent<LogicalItemHasVisual>(request.LogicalItemEntity).VisualEntity;
                        ecb.DestroyEntity(visualEntity);
                    }
                    ecb.DestroyEntity(request.LogicalItemEntity);
                }
                
                ecb.DestroyEntity(requestEntity);

            }).Run();
    }
}