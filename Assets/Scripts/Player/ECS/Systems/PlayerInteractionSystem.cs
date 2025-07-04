using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using UnityEngine;

/// <summary>
/// ECS-система, обрабатывающая взаимодействие игрока с объектами в мире.
/// Отвечает за определение сущностей, на которые смотрит игрок, и создание
/// соответствующих запросов на открытие UI (NPC, поселение).
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(InputsSystem))]
public partial class PlayerInteractionSystem : SystemBase
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
    /// Вызывается каждый кадр. Проверяет, был ли запрос на взаимодействие,
    /// выполняет трассировку луча от камеры и создает запросы UI
    /// в зависимости от того, с какой сущностью было взаимодействие.
    /// </summary>
    protected override void OnUpdate()
    {
        if (SystemAPI.GetSingleton<GameState>().CurrentMode != GameMode.Default) return;
        
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
                // Debug.Log($"[PlayerInteractionSystem] Создан запрос OpenNPCUIRequest для {interactedEntity}");
            }
            else if (em.HasComponent<SettlementComponent>(interactedEntity))
            {
                var requestEntity = ecb.CreateEntity();
                ecb.AddComponent(requestEntity, new OpenSettlementUIRequest { Target = interactedEntity });
                // Debug.Log($"[PlayerInteractionSystem] Создан запрос OpenSettlementUIRequest для {interactedEntity}");
            }
        }
    }
}