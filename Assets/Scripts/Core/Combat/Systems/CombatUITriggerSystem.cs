// Assets/Scripts/Core/Combat/Systems/CombatUITriggerSystem.cs
using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(DeathSystem))]
public partial class CombatUITriggerSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = SystemAPI
            .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

        // Гарантируем «вечный» синглтон UI
        Entity uiSingleton;
        if (!SystemAPI.TryGetSingletonEntity<ActiveCombatTarget>(out uiSingleton))
        {
            uiSingleton = ecb.CreateEntity();
            ecb.AddComponent(uiSingleton, new ActiveCombatTarget { TargetEntity = Entity.Null });
        }

        // Если нет игрока — просто скрываем UI
        if (!SystemAPI.TryGetSingletonEntity<PlayerTag>(out var playerEntity))
        {
            ecb.SetComponent(uiSingleton, new ActiveCombatTarget { TargetEntity = Entity.Null });
            return;
        }

        Entity hoveredTarget = Entity.Null;

        if (SystemAPI.HasComponent<ActiveTarget>(playerEntity) &&
            SystemAPI.HasComponent<ActiveEquippedItem>(playerEntity))
        {
            var activeTarget = SystemAPI.GetComponent<ActiveTarget>(playerEntity);
            var equippedItem = SystemAPI.GetComponent<ActiveEquippedItem>(playerEntity);
            var itemRegistry = ItemRegistry.Instance;

            if (itemRegistry != null &&
                EntityManager.Exists(activeTarget.Value) &&
                !SystemAPI.HasComponent<Disabled>(activeTarget.Value) &&
                !SystemAPI.HasComponent<IsDeadTag>(activeTarget.Value) &&
                SystemAPI.HasComponent<NPCComponent>(activeTarget.Value))
            {
                var itemData = itemRegistry.GetItemData(equippedItem.ItemID);
                if (itemData != null && itemData.itemType == ItemType.Weapon)
                    hoveredTarget = activeTarget.Value;
            }
        }

        // Только SetComponent — без Destroy/Remove
        ecb.SetComponent(uiSingleton, new ActiveCombatTarget { TargetEntity = hoveredTarget });
    }
}
