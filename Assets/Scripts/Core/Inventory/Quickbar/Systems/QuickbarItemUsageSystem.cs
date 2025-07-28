using Unity.Entities;
using UnityEngine;

/// <summary>
/// Система, которая реагирует на смену активного предмета в квикбаре.
/// Это система логики механик. Она не знает про ввод или инвентарь, она только смотрит
/// на результат их работы — компонент ActiveEquippedItem. На основе типа предмета в этом
/// компоненте, она отправляет запросы на изменение глобального состояния игры (например,
/// войти в режим строительства).
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ActiveItemSystem))]
public partial class QuickbarItemUsageSystem : SystemBase
{
    /// <summary>
    /// Вызывается при создании системы. Гарантирует, что синглтон GameState существует.
    /// </summary>
    protected override void OnCreate()
    {
        RequireForUpdate<GameState>();
    }

    /// <summary>
    /// Вызывается каждый кадр для обработки смены активного предмета.
    /// </summary>
    protected override void OnUpdate()
    {
        var gameStateEntity = SystemAPI.GetSingletonEntity<GameState>();
        
        // Если игра в данный момент находится в режиме UI, эта система не должна выполнять никаких действий.
        if (SystemAPI.HasComponent<InUIMode>(gameStateEntity))
        {
            return;
        }

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);
            
        var itemRegistry = ItemRegistry.Instance;
        if (itemRegistry == null) return;
        
        bool isInBuildMode = SystemAPI.HasComponent<InBuildingMode>(gameStateEntity);
        
        int currentBuildingId = -1;
        if (isInBuildMode)
        {
            currentBuildingId = SystemAPI.GetComponent<BuildingState>(gameStateEntity).BuildingItemID;
        }

        // Ключевая оптимизация: система работает, только если компонент ActiveEquippedItem изменился.
        Entities
            .WithAll<PlayerTag>()
            .WithChangeFilter<ActiveEquippedItem>()
            .WithoutBurst()
            .ForEach((Entity playerEntity) =>
            {
                // Случай 1: Игрок выбрал какой-то предмет.
                if (SystemAPI.HasComponent<ActiveEquippedItem>(playerEntity))
                {
                    var equippedItem = SystemAPI.GetComponent<ActiveEquippedItem>(playerEntity);
                    var itemData = itemRegistry.GetItemData(equippedItem.ItemID);

                    // Если выбранный предмет - это здание.
                    if (itemData != null && itemData.itemType == ItemType.Building)
                    {
                        // Если мы уже строим это же здание, ничего не делаем, чтобы не спамить запросы.
                        if (isInBuildMode && currentBuildingId == itemData.itemID)
                        {
                            return;
                        }

                        // Отправляем запрос на вход/смену режима строительства.
                        var requestEntity = ecb.CreateEntity();
                        ecb.AddComponent(requestEntity, new EnterBuildingModeRequest { ItemID = itemData.itemID });
                    }
                    // Если выбранный предмет - НЕ здание.
                    else
                    {
                        // А мы были в режиме строительства, значит, нужно из него выйти.
                        if (isInBuildMode)
                        {
                            var requestEntity = ecb.CreateEntity();
                            ecb.AddComponent<ExitBuildingModeRequest>(requestEntity);
                        }
                    }
                }
                // Случай 2: Игрок выбрал пустой слот.
                else
                {
                    // Если мы были в режиме строительства, выходим из него.
                    if (isInBuildMode)
                    {
                        var requestEntity = ecb.CreateEntity();
                        ecb.AddComponent<ExitBuildingModeRequest>(requestEntity);
                    }
                }
            }).Run();
    }
}