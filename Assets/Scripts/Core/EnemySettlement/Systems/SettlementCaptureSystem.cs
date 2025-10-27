using Unity.Entities;

/// <summary>
/// Система, отвечающая за логику захвата вражеских поселений игроком.
/// Она обрабатывает запросы на захват, проверяет, все ли враги,
/// связанные с поселением, уничтожены, и в случае успеха преобразует
/// вражеское поселение в объект, который может быть освоен игроком,
/// а также инициализирует для него инвентарь.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SettlementCaptureSystem : SystemBase
{
    /// <summary>
    /// Запрос для эффективного поиска всех живых врагов в мире.
    /// </summary>
    private EntityQuery aliveEnemiesQuery;

    /// <summary>
    /// Вызывается при создании системы. Инициализирует запрос для отслеживания
    /// живых врагов, чтобы не создавать его каждый кадр.
    /// </summary>
    protected override void OnCreate()
    {
        aliveEnemiesQuery = GetEntityQuery(
            ComponentType.ReadOnly<HostileNPCTag>(),
            ComponentType.Exclude<IsDeadTag>()
        );
    }
    
    /// <summary>
    /// Выполняется каждый кадр. Проверяет наличие запросов на захват и обрабатывает их.
    /// </summary>
    protected override void OnUpdate()
    {
        // Если нет активных запросов на захват, система ничего не делает.
        if (SystemAPI.QueryBuilder().WithAll<CaptureSettlementRequest>().Build().IsEmpty) return;

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);
            
        var spawnedByLookup = SystemAPI.GetComponentLookup<SpawnedBySettlement>(true);

        // Итерация по всем сущностям с запросом на захват.
        Entities.ForEach((Entity requestEntity, in CaptureSettlementRequest request) =>
        {
            // Проверяем, остались ли в живых враги, порожденные этим поселением.
            bool canCapture = true;
            var enemies = aliveEnemiesQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            foreach (var enemy in enemies)
            {
                if (spawnedByLookup.HasComponent(enemy) && spawnedByLookup[enemy].SettlementEntity == request.SettlementEntity)
                {
                    // Если найден хотя бы один живой враг, захват невозможен.
                    canCapture = false;
                    break;
                }
            }
            enemies.Dispose();

            // Если все враги уничтожены, производим захват.
            if (canCapture)
            {
                Entity settlement = request.SettlementEntity;
                // Удаляем компоненты, отвечающие за вражескую принадлежность и спавн.
                ecb.RemoveComponent<EnemySettlementTag>(settlement);
                ecb.RemoveComponent<EnemySpawnerComponent>(settlement);

                // Добавляем компоненты, помечающие поселение как потенциально игровое.
                ecb.AddComponent<PlayerSettlementCandidateTag>(settlement);
                ecb.AddComponent<NewlyBuiltTag>(settlement);
                
                // Инициализируем инвентарь для захваченного поселения.
                
                // Добавляем тег наличия инвентаря и компонент с его свойствами (вместимостью).
                ecb.AddComponent<HasInventoryTag>(settlement);
                int inventoryCapacity = 100; // Вместимость, может быть вынесена в настройки.
                ecb.AddComponent(settlement, new InventoryProperties { Capacity = inventoryCapacity });
                
                // Добавляем динамический буфер для хранения предметов.
                var inventoryBuffer = ecb.AddBuffer<InventoryItemElement>(settlement);
                
                // Резервируем место в буфере и заполняем его пустыми слотами.
                // Это важный шаг для корректной работы инвентарных систем.
                inventoryBuffer.ResizeUninitialized(inventoryCapacity);
                for (int i = 0; i < inventoryCapacity; i++)
                {
                    inventoryBuffer[i] = new InventoryItemElement { ItemID = 0, Amount = 0 };
                }
                
                // Создаем уведомление для UI об успешном захвате.
                var notification = ecb.CreateEntity();
                ecb.AddComponent(notification, new UINotificationRequest { Message = "Аванпост захвачен!" });
            }
            else
            {
                // Если враги еще живы, создаем уведомление о невозможности захвата.
                var notification = ecb.CreateEntity();
                ecb.AddComponent(notification, new UINotificationRequest { Message = "Невозможно захватить, пока живы враги!" });
            }

            // Уничтожаем обработанный запрос.
            ecb.DestroyEntity(requestEntity);

        }).WithoutBurst().Run();
    }
}