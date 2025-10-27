﻿using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Система, отвечающая за спавн врагов из вражеских поселений.
/// Периодически проверяет каждое поселение и, если количество активных врагов,
/// порожденных этим поселением, ниже максимума, создает нового врага
/// в случайной точке на NavMesh в пределах заданного радиуса.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class EnemySettlementSpawningSystem : SystemBase
{
    /// <summary>
    /// Запрос для эффективного подсчета всех живых врагов в мире.
    /// </summary>
    private EntityQuery aliveEnemiesQuery;

    /// <summary>
    /// Вызывается при создании системы. Инициализирует запрос для отслеживания
    /// живых врагов и устанавливает требование наличия игрока для работы системы.
    /// </summary>
    protected override void OnCreate()
    {
        aliveEnemiesQuery = GetEntityQuery(
            ComponentType.ReadOnly<HostileNPCTag>(),
            ComponentType.Exclude<IsDeadTag>()
        );
        RequireForUpdate<PlayerTag>();
    }

    /// <summary>
    /// Выполняется каждый кадр. Содержит основную логику спавна.
    /// Проходит по всем поселениям, проверяет таймеры и лимиты,
    /// находит валидную точку на NavMesh и создает команду на инстанцирование врага.
    /// </summary>
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);
        
        float currentTime = (float)SystemAPI.Time.ElapsedTime;
        var spawnedByLookup = SystemAPI.GetComponentLookup<SpawnedBySettlement>(true);

        // Entities.ForEach используется для итерации по всем сущностям с компонентом EnemySettlementTag.
        Entities
            .WithAll<EnemySettlementTag>()
            .WithoutBurst() // Используется, так как внутри цикла есть вызов управляемого кода (NavMesh.SamplePosition и Physics.Raycast).
            .ForEach((Entity settlementEntity, ref EnemySpawnerComponent spawner, in LocalToWorld settlementTransform) =>
            {
                // Если время для следующего спавна еще не наступило, выходим.
                if (currentTime < spawner.NextSpawnTime)
                {
                    return;
                }
                // Устанавливаем время следующего спавна.
                spawner.NextSpawnTime = currentTime + spawner.SpawnInterval;

                // Подсчитываем, сколько врагов уже было заспавнено этим конкретным поселением.
                int currentSpawnCount = 0;
                var enemies = aliveEnemiesQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                foreach (var enemy in enemies)
                {
                    if (spawnedByLookup.HasComponent(enemy) && spawnedByLookup[enemy].SettlementEntity == settlementEntity)
                    {
                        currentSpawnCount++;
                    }
                }
                enemies.Dispose();
                
                // Если лимит не превышен, пытаемся заспавнить нового врага.
                if (currentSpawnCount < spawner.MaxSpawnedEnemies)
                {
                    bool pointFound = false;
                    float3 spawnPosition = float3.zero;
                    var random = Unity.Mathematics.Random.CreateFromIndex((uint)(settlementEntity.Index + currentTime * 100));
                    
                    // Пытаемся найти валидную точку на NavMesh несколько раз.
                    const int maxAttempts = 20; // Увеличено для большей надежности.
                    for (int i = 0; i < maxAttempts; i++)
                    {
                        float2 randomCircle = random.NextFloat2Direction() * random.NextFloat(spawner.SpawnRadius * 0.5f, spawner.SpawnRadius);
                        float3 candidatePosition = settlementTransform.Position + new float3(randomCircle.x, 0, randomCircle.y);

                        // Выполняем raycast, чтобы найти высоту земли. Увеличены параметры для обработки неровного террейна.
                        RaycastHit raycastHit;
                        bool groundFound = Physics.Raycast(
                            new Vector3(candidatePosition.x, candidatePosition.y + 100.0f, candidatePosition.z), // Увеличен старт для надежности.
                            Vector3.down,
                            out raycastHit,
                            200.0f, // Увеличена дистанция.
                            LayerMask.GetMask("Ground") // Убедитесь, что слой "Ground" настроен в Unity для террейна.
                        );

                        #if UNITY_EDITOR
                        // Debug-визуализация raycast для отладки в Scene view.
                        Debug.DrawRay(new Vector3(candidatePosition.x, candidatePosition.y + 100.0f, candidatePosition.z), Vector3.down * 200.0f, Color.red, 1.0f);
                        #endif

                        float groundY = candidatePosition.y; // Default to current y if both methods fail.

                        if (groundFound)
                        {
                            groundY = raycastHit.point.y;
                            #if UNITY_EDITOR
                            Debug.Log($"Raycast нашел землю для кандидата {i} на высоте {groundY}.");
                            #endif
                        }
                        else
                        {
                            // Альтернатива: Используем Terrain.SampleHeight, если raycast не сработал.
                            Terrain terrain = Terrain.activeTerrain;
                            if (terrain != null)
                            {
                                groundY = terrain.SampleHeight(new Vector3(candidatePosition.x, 0, candidatePosition.z)) + terrain.transform.position.y;
                                groundFound = true;
                                #if UNITY_EDITOR
                                Debug.Log($"Terrain.SampleHeight нашел землю для кандидата {i} на высоте {groundY}.");
                                #endif
                            }
                            else
                            {
                                #if UNITY_EDITOR
                                Debug.LogWarning($"Ни raycast, ни Terrain.SampleHeight не нашли землю для кандидата {i}. Проверьте наличие Terrain в сцене.");
                                #endif
                            }
                        }

                        if (groundFound)
                        {
                            // Корректируем позицию кандидата на высоту земли.
                            candidatePosition.y = groundY;

                            // Проверяем, есть ли в этом месте NavMesh. Увеличен радиус для неровного террейна.
                            if (NavMesh.SamplePosition(candidatePosition, out NavMeshHit navMeshHit, 2.0f, NavMesh.AllAreas))
                            {
                                // Убеждаемся, что точка NavMesh близка к высоте земли.
                                if (Mathf.Abs(navMeshHit.position.y - candidatePosition.y) < 0.5f) // Допускаем небольшую разницу в высоте.
                                {
                                    spawnPosition = navMeshHit.position;
                                    pointFound = true;
                                    break;
                                }
                            }
                        }
                    }

                    // Фоллбек: Если точка не найдена, пытаемся спавнить случайно вокруг поселения с меньшим радиусом.
                    if (!pointFound)
                    {
                        float fallbackRadius = spawner.SpawnRadius * 0.5f; // Меньший радиус, чтобы избежать дальних спавнов.
                        const int fallbackAttempts = 10;
                        for (int i = 0; i < fallbackAttempts; i++)
                        {
                            float2 randomCircle = random.NextFloat2Direction() * random.NextFloat(0.5f, fallbackRadius); // От 0.5f чтобы не в центре.
                            float3 candidatePosition = settlementTransform.Position + new float3(randomCircle.x, 0, randomCircle.y);

                            RaycastHit raycastHit;
                            bool groundFound = Physics.Raycast(
                                new Vector3(candidatePosition.x, candidatePosition.y + 100.0f, candidatePosition.z),
                                Vector3.down,
                                out raycastHit,
                                200.0f,
                                LayerMask.GetMask("Ground")
                            );

                            float groundY = candidatePosition.y;

                            if (groundFound)
                            {
                                groundY = raycastHit.point.y;
                            }
                            else
                            {
                                Terrain terrain = Terrain.activeTerrain;
                                if (terrain != null)
                                {
                                    groundY = terrain.SampleHeight(new Vector3(candidatePosition.x, 0, candidatePosition.z)) + terrain.transform.position.y;
                                    groundFound = true;
                                }
                            }

                            if (groundFound)
                            {
                                candidatePosition.y = groundY;
                                if (NavMesh.SamplePosition(candidatePosition, out NavMeshHit navMeshHit, 2.0f, NavMesh.AllAreas))
                                {
                                    if (Mathf.Abs(navMeshHit.position.y - candidatePosition.y) < 0.5f)
                                    {
                                        spawnPosition = navMeshHit.position;
                                        pointFound = true;
                                        #if UNITY_EDITOR
                                        Debug.Log($"Фоллбек нашел точку вокруг поселения {settlementEntity} на попытке {i}.");
                                        #endif
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    // Если точка найдена, создаем врага.
                    if (pointFound)
                    {
                        Entity newEnemy = ecb.Instantiate(spawner.EnemyPrefab);
                        ecb.SetComponent(newEnemy, LocalTransform.FromPosition(spawnPosition + new float3(0f, 1f, 0f)));
                        ecb.AddComponent(newEnemy, new SpawnedBySettlement { SettlementEntity = settlementEntity });
                    }
                    else
                    {
                        // В противном случае выводим предупреждение (только в редакторе).
                        #if UNITY_EDITOR
                        Debug.LogWarning($"Не удалось найти валидную точку на земле с NavMesh для спавна врага из поселения {settlementEntity} после {maxAttempts} попыток и фоллбека. " +
                                         $"Проверьте запекание NavMesh, настройку слоя 'Ground', TerrainCollider и наличие Terrain в радиусе {spawner.SpawnRadius} от поселения.");
                        #endif
                    }
                }
            }).Run();
    }
}