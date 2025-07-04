using Unity.Entities;
using UnityEngine;
using Unity.Collections;

/// <summary>
/// Authoring-компонент для определения поселений в ECS.
/// Этот компонент должен находиться на префабе здания, которое является поселением.
/// </summary>
[DisallowMultipleComponent]
public class SettlementAuthoring : MonoBehaviour
{
    [Header("Settlement Settings")]
    public string settlementName;
    
    private const int STARTING_POPULATION = 0;
    private const int STARTING_LEVEL = 1;

    [Header("Player Settings")]
    [Tooltip("Отметьте, если это здание должно стать ГЛАВНЫМ поселением игрока, когда будет построено ПЕРВЫМ.")]
    public bool canBecomePlayerSettlement = true;

    /// <summary>
    /// Baker-класс, который добавляет SettlementComponent к сущности-префабу.
    /// </summary>
    private class Baker : Baker<SettlementAuthoring>
    {
        /// <summary>
        /// Выполняет процесс "запекания", добавляя компоненты к сущности префаба.
        /// </summary>
        /// <param name="authoring">Экземпляр Authoring-компонента.</param>
        public override void Bake(SettlementAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new SettlementComponent
            {
                Name = new FixedString64Bytes(authoring.settlementName),
                Population = STARTING_POPULATION,
                Level = STARTING_LEVEL,
                NPCs = new FixedList64Bytes<Entity>()
            });

            if (authoring.canBecomePlayerSettlement)
            {
                AddComponent<PlayerSettlementCandidateTag>(entity);
            }
        }
    }
}