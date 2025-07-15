using Unity.Entities;
using Unity.Physics;
using UnityEngine;

/// <summary>
/// Система, которая каждый кадр определяет, на какую сущность смотрит игрок,
/// и записывает эту информацию в универсальный компонент ActiveTarget.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class TargetDetectorSystem : SystemBase
{
    /// <summary>
    /// Вызывается при создании системы. Гарантирует, что система будет активна,
    /// только когда в мире существуют необходимые синглтоны.
    /// </summary>
    protected override void OnCreate()
    {
        RequireForUpdate<PlayerControllerData>();
        RequireForUpdate<PhysicsWorldSingleton>();
    }

    /// <summary>
    /// Вызывается каждый кадр. Выполняет трассировку луча от камеры для обнаружения цели.
    /// Если цель найдена, к сущности игрока добавляется (или обновляется) компонент <c>ActiveTarget</c>.
    /// Работает только тогда, когда игра не находится в режиме UI.
    /// </summary>
    protected override void OnUpdate()
    {
        if(!SystemAPI.TryGetSingletonEntity<PlayerControllerData>(out var playerEntity)) return;
        
        // Используем унифицированный, современный способ получения командного буфера.
        var ecbSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
        var ecb = ecbSystem.CreateCommandBuffer();

        // В начале каждого кадра удаляем старую цель, чтобы избежать устаревших данных.
        ecb.RemoveComponent<ActiveTarget>(playerEntity);

        // Если игра находится в режиме UI, прекращаем выполнение, чтобы не обнаруживать цели в мире.
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
            // Если луч попал в сущность, добавляем компонент ActiveTarget к игроку,
            // содержащий ссылку на эту сущность.
            ecb.AddComponent(playerEntity, new ActiveTarget { Value = hit.Entity });
        }
    }
}