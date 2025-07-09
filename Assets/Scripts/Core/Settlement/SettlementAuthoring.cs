using Unity.Entities;
using UnityEngine;
using Unity.Collections;

/// <summary>
/// Authoring-компонент для определения поселений в ECS.
/// </summary>
[DisallowMultipleComponent]
public class SettlementAuthoring : MonoBehaviour
{
    [Header("Settlement Settings")]
    public string settlementName;
    public int startingPopulation = 0;
    public int startingLevel = 1;
    
    [Header("Player Settings")]
    [Tooltip("Отметьте, если это здание должно стать ГЛАВНЫМ поселением игрока.")]
    public bool canBecomePlayerSettlement = true;

    // --- ДОБАВЛЕНО: НАСТРОЙКИ СКЛАДА ПОСЕЛЕНИЯ ---
    [Header("Storage Settings")]
    [Tooltip("Имеет ли это поселение собственный инвентарь (склад).")]
    public bool hasStorage = false;
    [Tooltip("Вместимость склада, если он есть.")]
    public int storageCapacity = 100;
    // ----------------------------------------------

    private class Baker : Baker<SettlementAuthoring>
    {
        public override void Bake(SettlementAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new SettlementComponent
            {
                Name = new FixedString64Bytes(authoring.settlementName),
                Population = authoring.startingPopulation,
                Level = authoring.startingLevel,
                NPCs = new FixedList64Bytes<Entity>()
            });

            if (authoring.canBecomePlayerSettlement)
            {
                AddComponent<PlayerSettlementCandidateTag>(entity);
            }
            
            // --- ДОБАВЛЕНО: ЛОГИКА СОЗДАНИЯ ИНВЕНТАРЯ ПОСЕЛЕНИЯ ---
            if (authoring.hasStorage)
            {
                AddComponent<HasInventoryTag>(entity);
                AddComponent(entity, new InventoryProperties { Capacity = authoring.storageCapacity });
                AddBuffer<InventoryItemElement>(entity);
            }
            // ----------------------------------------------------
        }
    }
}