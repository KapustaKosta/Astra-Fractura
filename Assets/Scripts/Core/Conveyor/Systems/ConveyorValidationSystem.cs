using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace Conveyor
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ConveyorPreviewPoseComputeSystem))]
    public partial class ConveyorValidationSystem : SystemBase
    {
        private const int GroundUnityLayer = 3;
        private const int ConnectorUnityLayer = 12;
        private const uint GroundCategoryBit = 1u << GroundUnityLayer;
        private const uint ConnectorCategoryBit = 1u << ConnectorUnityLayer;

        private static readonly CollisionFilter kBlockersFilter = new CollisionFilter
        {
            BelongsTo = ~0u,
            CollidesWith = ~(GroundCategoryBit | ConnectorCategoryBit),
            GroupIndex = 0
        };

        protected override void OnCreate()
        {
            RequireForUpdate<InConveyorMode>();
            RequireForUpdate<PhysicsWorldSingleton>();
            RequireForUpdate<PlayerTag>();
        }

        protected override void OnUpdate()
        {
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            var em = EntityManager;
            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                               .CreateCommandBuffer(World.Unmanaged);

            var st = SystemAPI.GetSingleton<ConveyorState>();
            var holder = st.PreviewEntity;

            if (!em.Exists(holder) || !em.HasBuffer<ConveyorLivePose>(holder))
            {
                if (em.Exists(holder) && em.HasComponent<ConveyorPlacementValidTag>(holder))
                    em.RemoveComponent<ConveyorPlacementValidTag>(holder);
                return;
            }

            var livePoses = em.GetBuffer<ConveyorLivePose>(holder);
            // Если сегментов 0, 1 или 2, валидация всегда успешна, так как проверять нечего.
            if (livePoses.Length <= 2)
            {
                if (!em.HasComponent<ConveyorPlacementValidTag>(holder))
                    em.AddComponent<ConveyorPlacementValidTag>(holder);
                return;
            }

            var ignoreBodies = new NativeList<int>(2, Allocator.Temp);

            if (em.Exists(st.StartConnector) && em.HasComponent<ConveyorConnector>(st.StartConnector))
            {
                var startOwner = em.GetComponentData<ConveyorConnector>(st.StartConnector).Owner;
                if (startOwner != Entity.Null && em.HasComponent<PhysicsCollider>(startOwner))
                {
                    int rb = physicsWorld.GetRigidBodyIndex(startOwner);
                    if (rb >= 0) ignoreBodies.Add(rb);
                }
            }

            if (SystemAPI.TryGetSingletonEntity<HoveredConnectorTag>(out var hovered) &&
                em.HasComponent<ConveyorConnector>(hovered))
            {
                var endOwner = em.GetComponentData<ConveyorConnector>(hovered).Owner;
                if (endOwner != Entity.Null && em.HasComponent<PhysicsCollider>(endOwner))
                {
                    int rb = physicsWorld.GetRigidBodyIndex(endOwner);
                    if (rb >= 0) ignoreBodies.Add(rb);
                }
            }

            bool valid = true;
            float3 halfExt = new float3(0.4f, 0.4f, 0f);
            var hits = new NativeList<DistanceHit>(Allocator.Temp);


            for (int i = 1; i < livePoses.Length - 1; i++)
            {
                var pose = livePoses[i];
                halfExt.z = pose.Length * 0.5f;

                hits.Clear();
                if (!physicsWorld.OverlapBox(pose.Position, pose.Rotation, halfExt, ref hits, kBlockersFilter))
                    continue;

                bool blocking = false;
                for (int h = 0; h < hits.Length; h++)
                {
                    int rb = hits[h].RigidBodyIndex;

                    bool ignored = false;
                    for (int j = 0; j < ignoreBodies.Length; j++)
                    {
                        if (ignoreBodies[j] == rb) { ignored = true; break; }
                    }
                    if (ignored) continue;

                    var hitEntity = physicsWorld.Bodies[rb].Entity;
                    if (em.Exists(hitEntity) && em.HasComponent<ConveyorGhostTag>(hitEntity))
                        continue;

                    blocking = true;
                    break;
                }

                if (blocking) { valid = false; break; }
            }


            if (valid)
            {
                int requiredAmount = livePoses.Length;
                if (requiredAmount > 0)
                {
                    var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
                    var playerInventory = SystemAPI.GetBuffer<InventoryItemElement>(playerEntity);
                    int currentAmount = 0;
                    int itemIDToConsume = st.ItemID;

                    foreach (var item in playerInventory)
                    {
                        if (item.ItemID == itemIDToConsume)
                        {
                            currentAmount += item.Amount;
                        }
                    }

                    if (currentAmount < requiredAmount)
                    {
                        valid = false;
                        var notification = ecb.CreateEntity();
                        ecb.AddComponent(notification, new UINotificationRequest { Message = $"Недостаточно конвейеров: {currentAmount} / {requiredAmount}" });
                    }
                }
            }

            if (valid)
            {
                if (!em.HasComponent<ConveyorPlacementValidTag>(holder))
                    em.AddComponent<ConveyorPlacementValidTag>(holder);
            }
            else
            {
                if (em.HasComponent<ConveyorPlacementValidTag>(holder))
                    em.RemoveComponent<ConveyorPlacementValidTag>(holder);
            }

            hits.Dispose();
            ignoreBodies.Dispose();
        }
    }
}