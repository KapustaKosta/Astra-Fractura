using Unity.Entities;

/// <summary>
/// Тег-компонент для идентификации сущности как вражеского поселения.
/// </summary>
public struct EnemySettlementTag : IComponentData { }

/// <summary>
/// Компонент, управляющий логикой спавна врагов из поселения.
/// </summary>
public struct EnemySpawnerComponent : IComponentData
{
    public Entity EnemyPrefab;
    public float SpawnInterval;
    public float NextSpawnTime;
    public int MaxSpawnedEnemies;
    public float SpawnRadius;
}

/// <summary>
/// Компонент на заспавненном враге со ссылкой на его "родное" поселение.
/// </summary>
public struct SpawnedBySettlement : IComponentData
{
    public Entity SettlementEntity;
}