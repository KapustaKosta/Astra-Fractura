using Unity.Entities;

/// <summary>
/// Центральная система, управляющая глобальным состоянием игры (GameMode).
/// Эта система является "мозгом", который решает, находится ли игрок в режиме игры,
/// строительства или взаимодействия с UI. Она слушает одноразовые запросы
/// (например, "открыть инвентарь" или "войти в режим строительства") и соответствующим
/// образом изменяет единый глобальный компонент (синглтон) GameState.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class GameStateSystem : SystemBase
{
    /// <summary>
    /// Вызывается при создании системы. Устанавливает требование наличия
    /// синглтона GameState для работы системы. Если GameState не будет найден,
    /// система не будет обновляться, что предотвратит ошибки.
    /// </summary>
    protected override void OnCreate()
    {
        RequireForUpdate<GameState>();
    }

    /// <summary>
    /// Вызывается каждый кадр. Обрабатывает все поступающие запросы,
    /// изменяя синглтон GameState в соответствии с ними.
    /// </summary>
    protected override void OnUpdate()
    {
        // Получаем доступ к единственному экземпляру GameState для чтения и записи.
        // Все изменения ниже будут применяться к этому одному компоненту.
        var gameState = SystemAPI.GetSingletonRW<GameState>();
        
        // ОБРАБОТКА ВХОДА В РЕЖИМ СТРОИТЕЛЬСТВА
        // Ищем все сущности, у которых есть компонент-запрос EnterBuildingModeRequest.
        Entities
            .WithoutBurst()
            .ForEach((in EnterBuildingModeRequest request) =>
            {
                // Если мы уже в режиме строительства, ничего не делаем.
                if (gameState.ValueRO.CurrentMode == GameMode.Building) return;

                // Находим ECS-префаб здания по ID предмета из запроса.
                Entity prefab = ItemToEntityResolver.GetEntityPrefabFromID(EntityManager, request.ItemID);
                if (prefab != Entity.Null)
                {
                    // Сохраняем текущий режим для логирования.
                    GameMode oldMode = gameState.ValueRO.CurrentMode;

                    // Переключаем игру в режим строительства.
                    gameState.ValueRW.CurrentMode = GameMode.Building;
                    // Сбрасываем все активные UI.
                    gameState.ValueRW.ActiveUIType = UIType.None;
                    gameState.ValueRW.ActiveUITarget = Entity.Null;
                    // Сохраняем в GameState, какой именно префаб здания мы собираемся строить.
                    gameState.ValueRW.BuildingPrefabToPlace = prefab;
                    gameState.ValueRW.BuildingItemID = request.ItemID;
                }
            })
            .Run();

        // ОБРАБОТКА ВЫХОДА ИЗ РЕЖИМА СТРОИТЕЛЬСТВА 
        // Ищем запросы на выход из режима (правая кнопка мыши) ИЛИ на размещение здания (левая кнопка).
        // И то, и другое должно возвращать игрока в обычный режим.
        Entities
            .WithAny<ExitBuildingModeRequest, PlaceBuildingRequest>()
            .WithoutBurst()
            .ForEach(() => 
            {
                // Если мы действительно были в режиме строительства.
                if (gameState.ValueRO.CurrentMode == GameMode.Building)
                {
                    // Возвращаемся в обычный игровой режим.
                    gameState.ValueRW.CurrentMode = GameMode.Default;
                    // Сбрасываем все данные, связанные со строительством.
                    gameState.ValueRW.ActiveUIType = UIType.None;
                    gameState.ValueRW.ActiveUITarget = Entity.Null;
                    gameState.ValueRW.BuildingPrefabToPlace = Entity.Null;
                    gameState.ValueRW.BuildingItemID = 0;
                }
            })
            .Run();

        // ОБРАБОТКА ПЕРЕКЛЮЧЕНИЯ ИНВЕНТАРЯ
        // Ищем запрос на открытие/закрытие инвентаря.
        Entities
            .WithoutBurst()
            .ForEach((in ToggleInventoryRequest request) =>
            {
                // Если инвентарь уже открыт (режим UI и тип UI - Inventory)
                if (gameState.ValueRO.CurrentMode == GameMode.UI && gameState.ValueRO.ActiveUIType == UIType.Inventory)
                {
                    // ...то закрываем его, возвращаясь в обычный режим.
                    gameState.ValueRW.CurrentMode = GameMode.Default;
                    gameState.ValueRW.ActiveUIType = UIType.None;
                    gameState.ValueRW.ActiveUITarget = Entity.Null;
                }
                else // Если мы были в любом другом режиме (Default, Building)
                {
                    // Переходим в режим UI и указываем, что активен именно инвентарь.
                    gameState.ValueRW.CurrentMode = GameMode.UI;
                    gameState.ValueRW.ActiveUIType = UIType.Inventory;
                    // У инвентаря нет конкретной цели (target), поэтому сбрасываем.
                    gameState.ValueRW.ActiveUITarget = Entity.Null; 
                    // Также сбрасываем данные о строительстве на случай, если инвентарь открыли из режима строительства.
                    gameState.ValueRW.BuildingPrefabToPlace = Entity.Null;
                    gameState.ValueRW.BuildingItemID = 0;
                }
            })
            .Run();

        // ОБРАБОТКА ОТКРЫТИЯ UI NPC 
        // Ищем запрос на открытие диалога с конкретным NPC.
        Entities
            .WithoutBurst()
            .ForEach((in OpenNPCUIRequest request) =>
            {
                // Переходим в режим UI, только если он еще не активен для этого же NPC.
                if (gameState.ValueRO.CurrentMode != GameMode.UI || gameState.ValueRO.ActiveUIType != UIType.NPC)
                {
                    // Переключаем игру в режим UI.
                    gameState.ValueRW.CurrentMode = GameMode.UI;
                    gameState.ValueRW.ActiveUIType = UIType.NPC;
                    // В GameState сохраняется, с КАКИМ ИМЕННО NPC мы взаимодействуем.
                    // request.Target - это сущность NPC, на которую кликнул игрок.
                    gameState.ValueRW.ActiveUITarget = request.Target;
                    gameState.ValueRW.BuildingPrefabToPlace = Entity.Null;
                    gameState.ValueRW.BuildingItemID = 0;
                }
            })
            .Run();
            
        // ОБРАБОТКА ОТКРЫТИЯ UI ПОСЕЛЕНИЯ
        // Ищем запрос на открытие меню конкретного поселения.
        Entities
            .WithoutBurst()
            .ForEach((in OpenSettlementUIRequest request) =>
            {
                 // Переходим в режим UI, только если он еще не активен для этого же поселения.
                 if (gameState.ValueRO.CurrentMode != GameMode.UI || gameState.ValueRO.ActiveUIType != UIType.Settlement)
                 {
                    // Переключаем игру в режим UI.
                    gameState.ValueRW.CurrentMode = GameMode.UI;
                    gameState.ValueRW.ActiveUIType = UIType.Settlement;
                    // Сохраняем сущность поселения, на которое кликнул игрок.
                    gameState.ValueRW.ActiveUITarget = request.Target;
                    gameState.ValueRW.BuildingPrefabToPlace = Entity.Null;
                    gameState.ValueRW.BuildingItemID = 0;
                 }
            })
            .Run();

        // БРАБОТКА ЗАКРЫТИЯ ВСЕХ UI
        // Ищем универсальный запрос на закрытие любого открытого UI (например, по нажатию Escape).
        Entities
            .WithoutBurst()
            .ForEach((in CloseAllUIRequest request) =>
            {
                 // Если мы находимся в любом из UI-режимов...
                 if (gameState.ValueRO.CurrentMode == GameMode.UI)
                 {
                    // ...возвращаемся в обычный игровой режим.
                    gameState.ValueRW.CurrentMode = GameMode.Default;
                    // Сбрасываем всю информацию об активном UI.
                    gameState.ValueRW.ActiveUIType = UIType.None;
                    gameState.ValueRW.ActiveUITarget = Entity.Null;
                 }
            })
            .Run();
    }
}