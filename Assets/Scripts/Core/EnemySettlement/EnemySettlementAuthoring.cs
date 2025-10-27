using Unity.Entities;
using UnityEngine;

public class EnemySettlementAuthoring : MonoBehaviour
{
    [Header("Основная информация")]
    public string settlementName = "Лагерь врагов";

    [Header("Настройки спавна")]
    [Tooltip("Префаб вражеского NPC, который будет спавниться.")]
    public GameObject enemyPrefab;
    [Tooltip("Как часто (в секундах) поселение будет пытаться заспавнить нового врага.")]
    public float spawnInterval = 30.0f;
    [Tooltip("Максимальное количество живых врагов от этого поселения.")]
    public int maxSpawnedEnemies = 5;
    [Tooltip("Радиус, в котором будут появляться враги.")]
    public float spawnRadius = 15.0f;

    class Baker : Baker<EnemySettlementAuthoring>
    {
        public override void Bake(EnemySettlementAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent<EnemySettlementTag>(entity);
            
            AddComponent(entity, new SettlementComponent
            {
                Name = new Unity.Collections.FixedString64Bytes(authoring.settlementName)
            });

            if (authoring.enemyPrefab == null)
            {
                Debug.LogError("Не назначен префаб врага для EnemySettlementAuthoring!", authoring);
                return;
            }

            AddComponent(entity, new EnemySpawnerComponent
            {
                EnemyPrefab = GetEntity(authoring.enemyPrefab, TransformUsageFlags.Dynamic),
                SpawnInterval = authoring.spawnInterval,
                NextSpawnTime = 0,
                MaxSpawnedEnemies = authoring.maxSpawnedEnemies,
                SpawnRadius = authoring.spawnRadius
            });
        }
    }
}