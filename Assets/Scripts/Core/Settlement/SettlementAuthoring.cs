using Unity.Entities;
using UnityEngine;
using Unity.Collections;

/// <summary>
/// Authoring-компонент для определения поселений в ECS.
/// Позволяет настраивать базовые параметры поселения.
/// Возможность иметь инвентарь (склад) теперь добавляется через компонент StartingInventoryAuthoring.
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
        }
    }
}