using Unity.Entities;
using UnityEngine;
using Unity.Collections;

/// <summary>
/// Authoring-компонент для определения поселений в ECS.
/// Позволяет настраивать базовые параметры поселения, а также наличие у него склада.
/// </summary>
[DisallowMultipleComponent]
public class SettlementAuthoring : MonoBehaviour
{
    [Header("Settlement Settings")]
    [Tooltip("Название поселения.")]
    public string settlementName;

    [Tooltip("Начальное количество жителей при постройке.")]
    public int startingPopulation = 0;

    [Tooltip("Начальный уровень поселения при постройке.")]
    public int startingLevel = 1;
    
    [Header("Player Settings")]
    [Tooltip("Отметьте, если это здание может стать ГЛАВНЫМ поселением игрока, когда будет построено ПЕРВЫМ.")]
    public bool canBecomePlayerSettlement = true;

    [Header("Storage Settings")]
    [Tooltip("Отметьте, если у этого поселения должен быть собственный инвентарь (склад).")]
    public bool hasStorage = false;
    
    [Tooltip("Вместимость склада в слотах, если он есть.")]
    public int storageCapacity = 100;

    /// <summary>
    /// Baker-класс, который преобразует данные из MonoBehaviour в ECS-компоненты.
    /// </summary>
    private class Baker : Baker<SettlementAuthoring>
    {
        /// <summary>
        /// Выполняет процесс "запекания", добавляя компоненты к сущности префаба.
        /// </summary>
        /// <param name="authoring">Экземпляр SettlementAuthoring.</param>
        public override void Bake(SettlementAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Добавляем основной компонент с данными поселения.
            AddComponent(entity, new SettlementComponent
            {
                Name = new FixedString64Bytes(authoring.settlementName),
                Population = authoring.startingPopulation,
                Level = authoring.startingLevel,
                NPCs = new FixedList64Bytes<Entity>()
            });

            // Если поселение может быть главным, добавляем тег-кандидат.
            if (authoring.canBecomePlayerSettlement)
            {
                AddComponent<PlayerSettlementCandidateTag>(entity);
            }
            
            // Если в инспекторе указано, что у поселения есть склад,
            // добавляем ему все необходимые для инвентаря компоненты.
            if (authoring.hasStorage)
            {
                AddComponent<HasInventoryTag>(entity);
                AddComponent(entity, new InventoryProperties { Capacity = authoring.storageCapacity });
                AddBuffer<InventoryItemElement>(entity);
            }
        }
    }
}