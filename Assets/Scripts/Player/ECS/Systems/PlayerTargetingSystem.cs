using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Система каждый кадр определяет, на какую сущность смотрит игрок,
/// и записывает эту информацию в компонент InteractionTarget.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class TargetDetectorSystem : SystemBase
{
    /// <summary>
    /// Вызывается при создании системы.
    /// </summary>
    protected override void OnCreate()
    {
        RequireForUpdate<PlayerControllerData>();
        RequireForUpdate<PhysicsWorldSingleton>();
    }

    /// <summary>
    /// Вызывается каждый кадр. Делает рейкаст и обновляет InteractionTarget у игрока.
    /// </summary>
    protected override void OnUpdate()
    {
        var playerEntity = SystemAPI.GetSingletonEntity<PlayerControllerData>();
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        
        // Сначала удаляем старую цель, если она была, чтобы избежать устаревших данных.
        ecb.RemoveComponent<InteractionTarget>(playerEntity);

        // Если мы в UI, не нужно определять цель в игровом мире.
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
            End = ray.origin + ray.direction * controllerData.TargetingDistance, // Дальность обнаружения цели
            Filter = CollisionFilter.Default
        };

        if (physicsWorld.CollisionWorld.CastRay(rayInput, out var hit))
        {
            // Если попали, добавляем/обновляем компонент с целью.
            ecb.AddComponent(playerEntity, new InteractionTarget { Value = hit.Entity });
        }
    }
}