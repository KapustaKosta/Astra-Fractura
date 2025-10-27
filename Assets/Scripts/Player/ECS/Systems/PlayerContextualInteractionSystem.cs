using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using UnityEngine;
using Energy.Core;
using Game.Production;
using Wiring;
using Game.Workshop;

/// <summary>
/// ЕДИНАЯ ECS-система, обрабатывающая взаимодействие игрока (ПКМ) с объектами в мире.
/// Работает только тогда, когда игра находится в режиме по умолчанию (InDefaultMode).
/// Централизованно обрабатывает все типы интерактивных сущностей.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class PlayerContextualInteractionSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<GameState>();
        RequireForUpdate<PhysicsWorldSingleton>();
        RequireForUpdate<PlayerInitializedTag>();
    }

    protected override void OnUpdate()
    {
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
        var controllerData = SystemAPI.GetSingleton<PlayerControllerData>();

        if (Camera.main == null) return;
        

        var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();


        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        var rayInput = new RaycastInput
        {
            Start = ray.origin,
            End = ray.origin + ray.direction * controllerData.InteractionDistance,
            Filter = new CollisionFilter
            {
                BelongsTo = ~0u,
                CollidesWith = (uint)controllerData.InteractableLayers,
                GroupIndex = 0
            }
        };

        if (physicsWorld.CollisionWorld.CastRay(rayInput, out var ecsHit))
        {
            Entity interactedEntity = ecsHit.Entity;
            var requestEntity = ecb.CreateEntity();

            if (em.HasComponent<NPCComponent>(interactedEntity))
            {
                ecb.AddComponent(requestEntity, new OpenNPCUIRequest { Target = interactedEntity });
            }
            else if (em.HasComponent<VisualFor>(interactedEntity))
            {
                var logicalEntity = em.GetComponentData<VisualFor>(interactedEntity).LogicalEntity;
                if (em.HasComponent<WorldItem>(logicalEntity))
                {
                    ecb.AddComponent(requestEntity, new PickupRequest
                    {
                        Player = playerEntity,
                        LogicalItemEntity = logicalEntity
                    });
                }
            }
            else if (em.HasComponent<SettlementComponent>(interactedEntity))
            {
                if (em.HasComponent<EnemySettlementTag>(interactedEntity))
                {
                    ecb.AddComponent(requestEntity, new OpenEnemySettlementUIRequest { Target = interactedEntity });
                }
                else
                {
                    ecb.AddComponent(requestEntity, new OpenSettlementUIRequest { Target = interactedEntity });
                }
            }
            else if (em.HasComponent<WorkshopTag>(interactedEntity))
            {
                ecb.AddComponent(requestEntity, new OpenWorkshopUIRequest { Target = interactedEntity });
            }
            else if (em.HasComponent<ProductionBuildingTag>(interactedEntity))
            {
                ecb.AddComponent(requestEntity, new OpenProductionUIRequest { Target = interactedEntity });
            }
            else if (em.HasComponent<GeneratorComponent>(interactedEntity))
            {
                ecb.AddComponent(requestEntity, new OpenGeneratorUIRequest { Target = interactedEntity });
            }
            else if (em.HasComponent<BatteryComponent>(interactedEntity))
            {
                ecb.AddComponent(requestEntity, new OpenBatteryUIRequest { Target = interactedEntity });
            }
            else if (em.HasComponent<QuarryTag>(interactedEntity))
            {
                ecb.AddComponent(requestEntity, new OpenQuarryUIRequest { Target = interactedEntity });
            }
            else
            {
                ecb.DestroyEntity(requestEntity);
            }
        }
    }
}