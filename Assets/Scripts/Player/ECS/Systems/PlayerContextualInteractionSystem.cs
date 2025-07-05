using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using UnityEngine;

/// <summary>
/// ECS-система, обрабатывающая взаимодействие игрока с объектами в мире.
/// Работает только тогда, когда игра находится в режиме по умолчанию.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(InputsSystem))]
public partial class PlayerContextualInteractionSystem : SystemBase
{
    /// <summary>
    /// Вызывается при создании системы. Требует наличия синглтона GameState,
    /// PhysicsWorldSingleton и PlayerInitializedTag для обновления.
    /// </summary>
    protected override void OnCreate()
    {
        RequireForUpdate<GameState>();
        RequireForUpdate<PhysicsWorldSingleton>();
        RequireForUpdate<PlayerInitializedTag>();
    }

    /// <summary>
    /// Вызывается каждый кадр. Проверяет, что игра в режиме InDefaultMode.
    /// Если был запрос на взаимодействие, выполняет трассировку луча от камеры
    /// и создает запросы UI в зависимости от того, с какой сущностью было взаимодействие.
    /// </summary>
    protected override void OnUpdate()
    {
        // Проверяем, что игра находится в нужном режиме. Если нет - выходим.
        // Это заменяет невалидное использование атрибута [WithAll] для SystemBase.
        var gameStateEntity = SystemAPI.GetSingletonEntity<GameState>();
        if (!SystemAPI.HasComponent<InDefaultMode>(gameStateEntity))
        {
            return;
        }
        
        var interactionRequestQuery = SystemAPI.QueryBuilder().WithAll<InteractionRequest>().Build();
        if (interactionRequestQuery.IsEmpty) return;

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        var em = EntityManager;

        if (Camera.main == null) return;
        
        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        var rayInput = new RaycastInput
        {
            Start = ray.origin,
            End = ray.origin + ray.direction * 5f,
            Filter = new CollisionFilter
            {
                BelongsTo = ~0u,
                CollidesWith = (uint)(1 << LayerMask.NameToLayer("NPC") | 1 << LayerMask.NameToLayer("Settlement")),
                GroupIndex = 0
            }
        };

        if (physicsWorld.CollisionWorld.CastRay(rayInput, out var ecsHit))
        {
            Entity interactedEntity = ecsHit.Entity;
            
            if (em.HasComponent<NPCComponent>(interactedEntity))
            {
                var requestEntity = ecb.CreateEntity();
                ecb.AddComponent(requestEntity, new OpenNPCUIRequest { Target = interactedEntity });
            }
            else if (em.HasComponent<SettlementComponent>(interactedEntity))
            {
                var requestEntity = ecb.CreateEntity();
                ecb.AddComponent(requestEntity, new OpenSettlementUIRequest { Target = interactedEntity });
            }
        }
    }
}