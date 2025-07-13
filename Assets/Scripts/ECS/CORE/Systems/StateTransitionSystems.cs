using Unity.Entities;
using UnityEngine;

/// <summary>
/// Система, которая обрабатывает запрос на вход в режим строительства.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class EnterBuildingModeSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().
            CreateCommandBuffer(World.Unmanaged);
        
        if (!SystemAPI.TryGetSingletonEntity<GameState>(out var gameStateEntity)) return;

        // Используем WithEntityAccess, чтобы получить Entity для логирования
        foreach (var (request, requestEntity) in 
                 SystemAPI.Query<RefRO<EnterBuildingModeRequest>>().WithEntityAccess())
        {
            
            Debug.Log($"[EnterBuildingModeSystem] Обнаружен запрос EnterBuildingModeRequest (Entity: {requestEntity.Index}," +
                      $" ItemID: {request.ValueRO.ItemID}).");

            if (SystemAPI.HasComponent<InBuildingMode>(gameStateEntity))
            {
                continue;
            }

            Entity prefab = ItemToEntityResolver.GetEntityPrefabFromID(EntityManager, request.ValueRO.ItemID);
            
            if (prefab != Entity.Null)
            {
                Debug.Log($"[EnterBuildingModeSystem] Префаб для ItemID {request.ValueRO.ItemID} успешно найден." +
                          $" Применяю смену состояния на InBuildingMode.");
                
                // Удаляем старые теги и данные
                ecb.RemoveComponent<InDefaultMode>(gameStateEntity);
                ecb.RemoveComponent<InUIMode>(gameStateEntity);
                ecb.RemoveComponent<UIState>(gameStateEntity);

                // Добавляем новые
                ecb.AddComponent<InBuildingMode>(gameStateEntity);
                ecb.AddComponent(gameStateEntity, new BuildingState
                {
                    BuildingPrefabToPlace = prefab,
                    BuildingItemID = request.ValueRO.ItemID
                });
            }
            // Ошибка об отсутствующем префабе теперь выводится из ItemToEntityResolver
        }
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
        var exitRequestQuery = SystemAPI.QueryBuilder().WithAny<ExitBuildingModeRequest, PlaceBuildingRequest>().Build();
        if (exitRequestQuery.IsEmpty) return;
        
        //Debug.Log("[ExitBuildingModeSystem] Обнаружен запрос на выход из режима строительства. Возврат в режим по умолчанию.");

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().
            CreateCommandBuffer(World.Unmanaged);
        var gameStateEntity = SystemAPI.GetSingletonEntity<GameState>();

        if (SystemAPI.HasComponent<InBuildingMode>(gameStateEntity))
        {
            ecb.RemoveComponent<InBuildingMode>(gameStateEntity);
            ecb.RemoveComponent<BuildingState>(gameStateEntity); 
            ecb.AddComponent<InDefaultMode>(gameStateEntity);
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

        
        //Debug.LogError("[!!!] ToggleInventorySystem сработала для смены состояния.");

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().
            CreateCommandBuffer(World.Unmanaged);
        var gameStateEntity = SystemAPI.GetSingletonEntity<GameState>();
        
        if (SystemAPI.HasComponent<InUIMode>(gameStateEntity) && 
            SystemAPI.GetComponent<UIState>(gameStateEntity).ActiveUIType == UIType.Inventory)
        {
            ecb.RemoveComponent<InUIMode>(gameStateEntity);
            ecb.RemoveComponent<UIState>(gameStateEntity);
            ecb.AddComponent<InDefaultMode>(gameStateEntity);
        }
        else 
        {
            ecb.RemoveComponent<InDefaultMode>(gameStateEntity);
            ecb.RemoveComponent<InBuildingMode>(gameStateEntity);
            ecb.RemoveComponent<BuildingState>(gameStateEntity);
            
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
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().
            CreateCommandBuffer(World.Unmanaged);
        var gameStateEntity = SystemAPI.GetSingletonEntity<GameState>();

        Entities.ForEach((in OpenNPCUIRequest request) =>
        {
            
            //Debug.LogWarning($"[OpenTargetedUISystem] Обнаружен запрос OpenNPCUIRequest для цели {request.Target}.");

            ecb.RemoveComponent<InDefaultMode>(gameStateEntity);
            ecb.RemoveComponent<InBuildingMode>(gameStateEntity);
            ecb.RemoveComponent<BuildingState>(gameStateEntity);
            
            ecb.AddComponent<InUIMode>(gameStateEntity);
            ecb.AddComponent(gameStateEntity, new UIState { ActiveUIType = UIType.NPC, ActiveUITarget = request.Target });

        }).Run();

        Entities.ForEach((in OpenSettlementUIRequest request) =>
        {
             
            //Debug.LogWarning($"[OpenTargetedUISystem] Обнаружен запрос OpenSettlementUIRequest для цели {request.Target}.");

            ecb.RemoveComponent<InDefaultMode>(gameStateEntity);
            ecb.RemoveComponent<InBuildingMode>(gameStateEntity);
            ecb.RemoveComponent<BuildingState>(gameStateEntity);

            ecb.AddComponent<InUIMode>(gameStateEntity);
            ecb.AddComponent(gameStateEntity, new UIState { ActiveUIType = UIType.Settlement, ActiveUITarget = request.Target });
        }).Run();
    }
}


/// <summary>
/// Система для обработки запроса на открытие окна торговли.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class OpenTradeUISystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().
            CreateCommandBuffer(World.Unmanaged);
        if (!SystemAPI.TryGetSingletonEntity<GameState>(out var gameStateEntity)) return;

        Entities.ForEach((in OpenTradeUIRequest request) =>
        {
            ecb.RemoveComponent<InDefaultMode>(gameStateEntity);
            ecb.RemoveComponent<InBuildingMode>(gameStateEntity);
            ecb.RemoveComponent<BuildingState>(gameStateEntity);
            ecb.RemoveComponent<UIState>(gameStateEntity); 

            ecb.AddComponent<InUIMode>(gameStateEntity);
            ecb.AddComponent(gameStateEntity, new UIState { ActiveUIType = UIType.Trade, ActiveUITarget = request.Target });

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
        
        
        //Debug.LogError("[!!!] CloseAllUISystem сработала для смены состояния.");

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().
            CreateCommandBuffer(World.Unmanaged);
        var gameStateEntity = SystemAPI.GetSingletonEntity<GameState>();

        if (SystemAPI.HasComponent<InUIMode>(gameStateEntity))
        {
            ecb.RemoveComponent<InUIMode>(gameStateEntity);
            ecb.RemoveComponent<UIState>(gameStateEntity);
            ecb.AddComponent<InDefaultMode>(gameStateEntity);
        }
    }
}