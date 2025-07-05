using Unity.Entities;

/// <summary>
/// Система, которая обрабатывает запрос на вход в режим строительства.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class EnterBuildingModeSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        var gameStateEntity = SystemAPI.GetSingletonEntity<GameState>();

        Entities
            .ForEach((in EnterBuildingModeRequest request) =>
            {
                // Игнорируем запрос, если мы уже в режиме строительства
                if (SystemAPI.HasComponent<InBuildingMode>(gameStateEntity)) return;

                Entity prefab = ItemToEntityResolver.GetEntityPrefabFromID(EntityManager, request.ItemID);
                if (prefab != Entity.Null)
                {
                    // Удаляем старые теги режимов
                    ecb.RemoveComponent<InDefaultMode>(gameStateEntity);
                    ecb.RemoveComponent<InUIMode>(gameStateEntity);
                    ecb.RemoveComponent<UIState>(gameStateEntity); // и данные UI

                    // Добавляем новые
                    ecb.AddComponent<InBuildingMode>(gameStateEntity);
                    ecb.AddComponent(gameStateEntity, new BuildingState
                    {
                        BuildingPrefabToPlace = prefab,
                        BuildingItemID = request.ItemID
                    });
                }
            }).WithoutBurst().Run();
    }
}

/// <summary>
/// Система, которая обрабатывает выход из режима строительства (по отмене или постройке).
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class ExitBuildingModeSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Этот запрос создается как при отмене (RMB), так и при успешном размещении здания
        var exitRequestQuery = SystemAPI.QueryBuilder().WithAny<ExitBuildingModeRequest, PlaceBuildingRequest>().Build();
        if (exitRequestQuery.IsEmpty) return;
        
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        var gameStateEntity = SystemAPI.GetSingletonEntity<GameState>();

        // Если мы действительно были в режиме строительства
        if (SystemAPI.HasComponent<InBuildingMode>(gameStateEntity))
        {
            ecb.RemoveComponent<InBuildingMode>(gameStateEntity);
            ecb.RemoveComponent<BuildingState>(gameStateEntity); // Удаляем специфичные данные
            ecb.AddComponent<InDefaultMode>(gameStateEntity);    // Возвращаемся в режим по умолчанию
        }
    }
}

/// <summary>
/// Система, которая обрабатывает открытие/закрытие инвентаря.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class ToggleInventorySystem : SystemBase
{
    protected override void OnUpdate()
    {
        var requestQuery = SystemAPI.QueryBuilder().WithAll<ToggleInventoryRequest>().Build();
        if (requestQuery.IsEmpty) return;

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        var gameStateEntity = SystemAPI.GetSingletonEntity<GameState>();
        
        // Если мы уже в режиме UI и это инвентарь - закрываем его
        if (SystemAPI.HasComponent<InUIMode>(gameStateEntity) && SystemAPI.GetComponent<UIState>(gameStateEntity).ActiveUIType == UIType.Inventory)
        {
            ecb.RemoveComponent<InUIMode>(gameStateEntity);
            ecb.RemoveComponent<UIState>(gameStateEntity);
            ecb.AddComponent<InDefaultMode>(gameStateEntity);
        }
        else // Иначе (мы в Default или Building) - открываем инвентарь
        {
            // Удаляем старые теги и данные
            ecb.RemoveComponent<InDefaultMode>(gameStateEntity);
            ecb.RemoveComponent<InBuildingMode>(gameStateEntity);
            ecb.RemoveComponent<BuildingState>(gameStateEntity);
            
            // Добавляем новые
            ecb.AddComponent<InUIMode>(gameStateEntity);
            ecb.AddComponent(gameStateEntity, new UIState
            {
                ActiveUIType = UIType.Inventory,
                ActiveUITarget = Entity.Null
            });
        }
    }
}


/// <summary>
/// Универсальная система для открытия окон UI, требующих цель (NPC, Поселение).
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class OpenTargetedUISystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        var gameStateEntity = SystemAPI.GetSingletonEntity<GameState>();

        // Обработка открытия UI для NPC
        Entities.ForEach((in OpenNPCUIRequest request) =>
        {
            // Удаляем старые теги и данные
            ecb.RemoveComponent<InDefaultMode>(gameStateEntity);
            ecb.RemoveComponent<InBuildingMode>(gameStateEntity);
            ecb.RemoveComponent<BuildingState>(gameStateEntity);
            
            // Добавляем новые
            ecb.AddComponent<InUIMode>(gameStateEntity);
            ecb.AddComponent(gameStateEntity, new UIState { ActiveUIType = UIType.NPC, ActiveUITarget = request.Target });

        }).Run();

        // Обработка открытия UI для Поселения
        Entities.ForEach((in OpenSettlementUIRequest request) =>
        {
            // Удаляем старые теги и данные
            ecb.RemoveComponent<InDefaultMode>(gameStateEntity);
            ecb.RemoveComponent<InBuildingMode>(gameStateEntity);
            ecb.RemoveComponent<BuildingState>(gameStateEntity);

            // Добавляем новые
            ecb.AddComponent<InUIMode>(gameStateEntity);
            ecb.AddComponent(gameStateEntity, new UIState { ActiveUIType = UIType.Settlement, ActiveUITarget = request.Target });
        }).Run();
    }
}


/// <summary>
/// Система, которая обрабатывает универсальный запрос на закрытие всех UI.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class CloseAllUISystem : SystemBase
{
    protected override void OnUpdate()
    {
        var requestQuery = SystemAPI.QueryBuilder().WithAll<CloseAllUIRequest>().Build();
        if (requestQuery.IsEmpty) return;
        
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        var gameStateEntity = SystemAPI.GetSingletonEntity<GameState>();

        // Если мы были в режиме UI
        if (SystemAPI.HasComponent<InUIMode>(gameStateEntity))
        {
            ecb.RemoveComponent<InUIMode>(gameStateEntity);
            ecb.RemoveComponent<UIState>(gameStateEntity);
            ecb.AddComponent<InDefaultMode>(gameStateEntity);
        }
    }
}