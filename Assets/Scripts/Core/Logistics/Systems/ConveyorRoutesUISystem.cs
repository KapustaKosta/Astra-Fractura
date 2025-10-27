using Unity.Entities;
using Conveyor;
using UnityEngine; 

/// <summary>
/// Система, которая служит мостом между пользовательским интерфейсом (UI) и логикой конвейерных маршрутов.
/// Она обрабатывает запросы, создаваемые UI (например, нажатиями кнопок),
/// и преобразует их в изменения компонентов ECS, такие как смена предмета на маршруте или его включение/выключение.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class ConveyorRoutesUISystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem.Singleton _endSimEcb;

    /// <summary>
    /// Вызывается при создании системы. Кэширует синглтон EndSimulationEntityCommandBufferSystem
    /// для повышения производительности, чтобы не искать его в каждом кадре.
    /// </summary>
    protected override void OnCreate()
    {
        _endSimEcb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
    }

    /// <summary>
    /// Выполняется каждый кадр. Обрабатывает различные UI-запросы.
    /// </summary>
    protected override void OnUpdate()
    {
        var ecb = _endSimEcb.CreateCommandBuffer(World.Unmanaged);

        // Обработка запроса на открытие окна UI с маршрутами.
        Entities.ForEach((Entity e, in OpenConveyorRoutesUIRequest req) =>
        {
            if (ConveyorRoutesUI.Instance != null)
            {
                ConveyorRoutesUI.Instance.Show();
            }
            ecb.DestroyEntity(e); 
        }).WithoutBurst().Run();

        // Обработка запроса на установку (или смену) предмета для конкретного маршрута.
        Entities
            .ForEach((Entity e, in SetRouteItemRequest req) =>
            {
                // Проверяем, что сущность маршрута все еще валидна.
                if (!SystemAPI.HasComponent<RouteDefinition>(req.RouteEntity) ||
                    !SystemAPI.HasBuffer<RoutePathElement>(req.RouteEntity))
                {
                    ecb.DestroyEntity(e);
                    return;
                }

                // Обновляем ItemID в определении маршрута.
                var routeDef = SystemAPI.GetComponent<RouteDefinition>(req.RouteEntity);
                routeDef.ItemID = req.NewItemID;
                ecb.SetComponent(req.RouteEntity, routeDef);

                // Если установлен новый предмет (ID > 0), настраиваем или сбрасываем таймер.
                if (req.NewItemID > 0)
                {
                    // Устанавливаем фиксированную задержку между отправкой предметов,
                    // чтобы создать видимый зазор между кубами на ленте.
                    // Значение 1.2f подобрано так, чтобы зазор был примерно в два раза больше длины самого куба.
                    // Вы можете увеличить это значение для большего расстояния или уменьшить для меньшего.
                    const float cooldown = 1.2f;

                    // Готовим таймер, но маршрут НЕ активируем здесь.
                    if (SystemAPI.HasComponent<RouteTimer>(req.RouteEntity))
                    {
                        ecb.SetComponent(req.RouteEntity, new RouteTimer { Cooldown = cooldown, TimeToNextTransfer = 0 });
                    }
                    else
                    {
                        ecb.AddComponent(req.RouteEntity, new RouteTimer { Cooldown = cooldown, TimeToNextTransfer = 0 });
                    }
                    // Сбрасываем тег активности, чтобы система активации заново проверила условия.
                    if (SystemAPI.HasComponent<ActiveRouteTag>(req.RouteEntity))
                    {
                        ecb.RemoveComponent<ActiveRouteTag>(req.RouteEntity);
                    }
                }
                // Если предмет сброшен (ID = 0), полностью деактивируем маршрут.
                else
                {
                    if (SystemAPI.HasComponent<RouteTimer>(req.RouteEntity))
                    {
                        ecb.RemoveComponent<RouteTimer>(req.RouteEntity);
                    }
                    if (SystemAPI.HasComponent<ActiveRouteTag>(req.RouteEntity))
                    {
                        ecb.RemoveComponent<ActiveRouteTag>(req.RouteEntity);
                    }
                    if (SystemAPI.HasComponent<UserEnabledRouteTag>(req.RouteEntity))
                    {
                        ecb.RemoveComponent<UserEnabledRouteTag>(req.RouteEntity);
                    }
                }

                ecb.DestroyEntity(e);
            }).Schedule();

        // Обработка запроса на включение/выключение (старт/пауза) маршрута пользователем.
        Entities.ForEach((Entity e, in ToggleRouteRequest req) =>
        {
            // Если маршрут уже был включен пользователем, выключаем его.
            if (SystemAPI.HasComponent<UserEnabledRouteTag>(req.RouteEntity))
            {
                ecb.RemoveComponent<UserEnabledRouteTag>(req.RouteEntity);
                // Также снимаем тег фактической активности, если он был.
                if (SystemAPI.HasComponent<ActiveRouteTag>(req.RouteEntity))
                {
                    ecb.RemoveComponent<ActiveRouteTag>(req.RouteEntity);
                }
                Debug.Log($"<color=cyan>[DEBUG/UI]</color> Запрос на ВЫКЛЮЧЕНИЕ маршрута {req.RouteEntity}. Убираем UserEnabledRouteTag.");
            }
            // Если маршрут был выключен, включаем его.
            else
            {
                // Маршрут можно включить только если для него настроен предмет (и, следовательно, есть таймер).
                if (SystemAPI.HasComponent<RouteTimer>(req.RouteEntity))
                {
                    ecb.AddComponent<UserEnabledRouteTag>(req.RouteEntity);
                    Debug.Log($"<color=cyan>[DEBUG/UI]</color> Запрос на ВКЛЮЧЕНИЕ маршрута {req.RouteEntity}. Добавляем UserEnabledRouteTag.");
                }
            }
            ecb.DestroyEntity(e);
        }).Schedule();
    }
}