using Unity.Entities;
using Conveyor;
using Unity.Collections;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class ConveyorRoutesUISystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem.Singleton _endSimEcb;

    protected override void OnCreate()
    {
        _endSimEcb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
    }

    protected override void OnUpdate()
    {
        var ecb = _endSimEcb.CreateCommandBuffer(World.Unmanaged);

        Entities.ForEach((Entity e, in OpenConveyorRoutesUIRequest req) =>
        {
            if (ConveyorRoutesUI.Instance != null)
            {
                ConveyorRoutesUI.Instance.Show();
            }
            ecb.DestroyEntity(e);
        }).WithoutBurst().Run();

        // ВЫБОР ПРЕДМЕТА БОЛЬШЕ НЕ ВКЛЮЧАЕТ МАРШРУТ! Только настраивает таймер.
        // ИСПРАВЛЕНИЕ: Мы больше не используем segmentSettingsLookup, поэтому удаляем .WithReadOnly()
        Entities
            .ForEach((Entity e, in SetRouteItemRequest req) =>
            {
                if (!SystemAPI.HasComponent<RouteDefinition>(req.RouteEntity) ||
                    !SystemAPI.HasBuffer<RoutePathElement>(req.RouteEntity))
                {
                    ecb.DestroyEntity(e);
                    return;
                }

                var routeDef = SystemAPI.GetComponent<RouteDefinition>(req.RouteEntity);
                routeDef.ItemID = req.NewItemID;
                ecb.SetComponent(req.RouteEntity, routeDef);

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

                    // Убедимся, что маршруту не присвоен ActiveRouteTag на этом шаге
                    if (SystemAPI.HasComponent<ActiveRouteTag>(req.RouteEntity))
                    {
                        ecb.RemoveComponent<ActiveRouteTag>(req.RouteEntity);
                    }
                }
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
                }

                ecb.DestroyEntity(e);
            }).Schedule();

        // Явный старт/пауза маршрута — здесь, как и раньше
        Entities.ForEach((Entity e, in ToggleRouteRequest req) =>
        {
            if (SystemAPI.HasComponent<ActiveRouteTag>(req.RouteEntity))
            {
                ecb.RemoveComponent<ActiveRouteTag>(req.RouteEntity);
            }
            else
            {
                if (SystemAPI.HasComponent<RouteTimer>(req.RouteEntity))
                {
                    ecb.AddComponent<ActiveRouteTag>(req.RouteEntity);
                }
            }
            ecb.DestroyEntity(e);
        }).Schedule();
    }
}
