using Unity.Entities;
using UnityEngine;
using Unity.Collections;

[DisallowMultipleComponent]
public class SettlementAuthoring : MonoBehaviour
{
    [Header("Settlement Settings")]
    public string settlementName; // Название поселения  
    public int initialPopulation = 0; // Начальное количество жителей  
    public int initialLevel = 1; // Начальный уровень поселения  

    class Baker : Baker<SettlementAuthoring>
    {
        public override void Bake(SettlementAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Добавляем компонент SettlementComponent с начальными значениями  
            AddComponent(entity, new SettlementComponent
            {
                Name = authoring.settlementName ?? string.Empty,
                Population = authoring.initialPopulation,
                Level = authoring.initialLevel,
                NPCs = new FixedList64Bytes<Entity>()
            });
        }
    }
}

public struct SettlementComponent : IComponentData
{
    public FixedString64Bytes Name; // Название поселения  
    public int Population; // Количество жителей  
    public int Level; // Уровень поселения  
    public FixedList64Bytes<Entity> NPCs; // Список нанятых NPC  
}

