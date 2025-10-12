using Unity.Entities;
using Unity.Physics;
using UnityEngine;

/// <summary>
/// Система, которая каждый кадр определяет, на какую сущность смотрит игрок,
/// и записывает эту информацию в универсальный компонент ActiveTarget.
/// Также создает синглтон HoveredItem, если цель является подбираемым предметом.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class TargetDetectorSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<PlayerControllerData>();
        RequireForUpdate<PhysicsWorldSingleton>();
    }

    protected override void OnUpdate()
    {
        if(!SystemAPI.TryGetSingletonEntity<PlayerControllerData>(out var playerEntity)) return;
        
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);


        // Удаляем старые цели БЕЗОПАСНЫМ способом.
        
        // 1. Проверяем, есть ли ActiveTarget, ПРЕЖДЕ чем пытаться его удалить.
        if (HasComponent<ActiveTarget>(playerEntity))
        {
            ecb.RemoveComponent<ActiveTarget>(playerEntity);
        }
        
        // 2. Эта проверка уже была безопасной благодаря TryGetSingletonEntity.
        if (SystemAPI.TryGetSingletonEntity<HoveredItem>(out var oldHoveredEntity))
        {
            ecb.DestroyEntity(oldHoveredEntity);
        }

        if (SystemAPI.HasComponent<InUIMode>(SystemAPI.GetSingletonEntity<GameState>()))
        {
            return;
        }

        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        var controllerData = SystemAPI.GetSingleton<PlayerControllerData>();
        if (Camera.main == null) return;

        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        // Используем универсальный фильтр, так как эта система не знает, что именно она ищет.
        // Она просто сообщает, во что попал луч.
        var rayInput = new RaycastInput
        {
            Start = ray.origin,
            End = ray.origin + ray.direction * controllerData.TargetingDistance,
            Filter = CollisionFilter.Default
        };

        if (physicsWorld.CollisionWorld.CastRay(rayInput, out var hit))
        {
            Entity hitEntity = hit.Entity;
            
            ecb.AddComponent(playerEntity, new ActiveTarget { Value = hitEntity });

            if (SystemAPI.HasComponent<VisualFor>(hitEntity))
            {
                var logicalEntity = SystemAPI.GetComponent<VisualFor>(hitEntity).LogicalEntity;
                if (SystemAPI.HasComponent<WorldItem>(logicalEntity))
                {
                    var worldItem = SystemAPI.GetComponent<WorldItem>(logicalEntity);
                    
                    var newHovered = ecb.CreateEntity();
                    ecb.AddComponent(newHovered, new HoveredItem
                    {
                        LogicalEntity = logicalEntity,
                        VisualEntity = hitEntity,
                        ItemID = worldItem.ItemID,
                        Amount = worldItem.Count
                    });
                }
            }
        }
    }
}