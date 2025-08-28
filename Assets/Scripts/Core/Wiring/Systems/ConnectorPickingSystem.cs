using Conveyor;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using URay = UnityEngine.Ray;

namespace Wiring
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ConnectorVisibilitySystem))]
    public partial class ConnectorPickingSystem : SystemBase
    {
        private const float kPickRadius = 0.35f;

        protected override void OnCreate()
        {
            RequireForUpdate<PhysicsWorldSingleton>();
            RequireForUpdate<WireConnector>();
            RequireForUpdate<WirePreviewConfig>();
            RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            RequireForUpdate<PlayerTag>();
        }

        protected override void OnUpdate()
        {
            if (!SystemAPI.HasSingleton<InWirePlacementMode>()) return;

            var cam = Camera.main;
            if (cam == null) return;

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                               .CreateCommandBuffer(World.Unmanaged);
            var pws = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            Entity clickedConnector = PickConnector(ray, in pws);

            if (Input.GetMouseButtonDown(1))
            {
                if (SystemAPI.TryGetSingletonEntity<PendingWireRemoval>(out var pendingRemovalEntity))
                {
                    HandleRemovalConfirmationOrCancellation(ecb, pendingRemovalEntity, clickedConnector);
                }
                else if (clickedConnector != Entity.Null)
                {
                    HandleRemovalInitiation(ecb, clickedConnector);
                }
                else if (SystemAPI.TryGetSingletonEntity<PendingWire>(out var pendingPlacementEntity))
                {
                    ecb.DestroyEntity(pendingPlacementEntity);
                }
            }

            if (Input.GetMouseButtonDown(0))
            {
                if (clickedConnector == Entity.Null) return;

                if (SystemAPI.TryGetSingletonEntity<PendingWire>(out var pendingPlacementEntity))
                {
                    HandleSecondClickPlacement(ecb, pendingPlacementEntity, clickedConnector);
                }
                else
                {
                    HandlePlacementInitiation(ecb, clickedConnector);
                }
            }
        }

        private void HandleRemovalInitiation(EntityCommandBuffer ecb, Entity clickedConnector)
        {
            if (SystemAPI.HasBuffer<ConnectedWires>(clickedConnector) &&
                SystemAPI.GetBuffer<ConnectedWires>(clickedConnector).Length > 0)
            {
                Entity wireEntity = SystemAPI.GetBuffer<ConnectedWires>(clickedConnector)[0].Wire;
                Entity visualEntity = FindVisualForWire(wireEntity);

                if (visualEntity != Entity.Null)
                {
                    var newEntity = ecb.CreateEntity();
                    ecb.AddComponent(newEntity, new PendingWireRemoval
                    {
                        FirstConnector = clickedConnector,
                        WireToRemove = wireEntity,
                        WireVisual = visualEntity
                    });
                }
            }
        }

        private void HandleRemovalConfirmationOrCancellation(EntityCommandBuffer ecb, Entity pendingEntity, Entity clickedConnector)
        {
            var pending = SystemAPI.GetComponent<PendingWireRemoval>(pendingEntity);
            if (!SystemAPI.HasComponent<Wire>(pending.WireToRemove))
            {
                ecb.DestroyEntity(pendingEntity);
                return;
            }

            var wireData = SystemAPI.GetComponent<Wire>(pending.WireToRemove);
            Entity otherEnd = (wireData.StartConnector == pending.FirstConnector) ? wireData.EndConnector : wireData.StartConnector;

            if (clickedConnector == otherEnd)
            {
                var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
                var itemRegistry = ItemRegistry.Instance;
                int wireItemId = FindWireItemIdByLevel(itemRegistry, wireData.Level);

                if (wireItemId != -1)
                {
                    var addItemRequestEntity = ecb.CreateEntity();
                    ecb.AddComponent(addItemRequestEntity, new AddItemRequest
                    {
                        TargetInventoryOwner = playerEntity,
                        ItemID = wireItemId,
                        Amount = 1
                    });
                }

                RemoveWireFromConnectorBuffer(wireData.StartConnector, pending.WireToRemove);
                RemoveWireFromConnectorBuffer(wireData.EndConnector, pending.WireToRemove);

                ecb.DestroyEntity(pending.WireVisual);
                ecb.DestroyEntity(pending.WireToRemove);

                if (!SystemAPI.HasSingleton<NetworkTopologyChanged>())
                {
                    var triggerEntity = ecb.CreateEntity();
                    ecb.AddComponent<NetworkTopologyChanged>(triggerEntity);
                }
            }

            ecb.DestroyEntity(pendingEntity);
        }

        private void RemoveWireFromConnectorBuffer(Entity connector, Entity wireToRemove)
        {
            if (!SystemAPI.HasBuffer<ConnectedWires>(connector)) return;
            var buffer = SystemAPI.GetBuffer<ConnectedWires>(connector);
            for (int i = buffer.Length - 1; i >= 0; i--)
            {
                if (buffer[i].Wire == wireToRemove)
                {
                    buffer.RemoveAt(i);
                    return;
                }
            }
        }

        private void HandlePlacementInitiation(EntityCommandBuffer ecb, Entity clickedConnector)
        {
            bool hasConnections = SystemAPI.HasBuffer<ConnectedWires>(clickedConnector) &&
                                  SystemAPI.GetBuffer<ConnectedWires>(clickedConnector).Length > 0;
            if (hasConnections) return;

            var wireState = SystemAPI.GetSingleton<WireState>();
            var newEntity = ecb.CreateEntity();
            ecb.AddComponent(newEntity, new PendingWire
            {
                StartConnector = clickedConnector,
                Level = wireState.SelectedWireLevel
            });
        }

        private void HandleSecondClickPlacement(EntityCommandBuffer ecb, Entity pendingEntity, Entity endConnector)
        {
            var pending = SystemAPI.GetComponent<PendingWire>(pendingEntity);
            var startConnector = pending.StartConnector;
            if (startConnector == endConnector || (SystemAPI.HasBuffer<ConnectedWires>(endConnector) && SystemAPI.GetBuffer<ConnectedWires>(endConnector).Length > 0))
            {
                ecb.DestroyEntity(pendingEntity);
                return;
            }

            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var itemRegistry = ItemRegistry.Instance;
            int wireItemId = FindWireItemIdByLevel(itemRegistry, pending.Level);

            if (wireItemId == -1 || !HasItemInInventory(SystemAPI.GetBuffer<InventoryItemElement>(playerEntity), wireItemId))
            {
                if (wireItemId != -1)
                {
                    var notificationEntity = ecb.CreateEntity();
                    ecb.AddComponent(notificationEntity, new UINotificationRequest { Message = "Недостаточно проводов!" });
                }
                ecb.DestroyEntity(pendingEntity);
                return;
            }

            var removeItemRequestEntity = ecb.CreateEntity();
            ecb.AddComponent(removeItemRequestEntity, new RemoveItemRequest { TargetInventoryOwner = playerEntity, ItemID = wireItemId, Amount = 1 });

            var wireLogic = ecb.CreateEntity();
            ecb.AddComponent(wireLogic, new Wire { StartConnector = startConnector, EndConnector = endConnector, Level = pending.Level });
            ecb.AppendToBuffer(startConnector, new ConnectedWires { Wire = wireLogic });
            ecb.AppendToBuffer(endConnector, new ConnectedWires { Wire = wireLogic });

            CreateWireVisual(ecb, wireLogic, startConnector, endConnector);
            ecb.DestroyEntity(pendingEntity);

            if (!SystemAPI.HasSingleton<NetworkTopologyChanged>())
            {
                var triggerEntity = ecb.CreateEntity();
                ecb.AddComponent<NetworkTopologyChanged>(triggerEntity);
            }
        }

        private void CreateWireVisual(EntityCommandBuffer ecb, Entity wireLogic, Entity start, Entity end)
        {
            if (SystemAPI.HasComponent<LocalToWorld>(start) && SystemAPI.HasComponent<LocalToWorld>(end))
            {
                float3 a = SystemAPI.GetComponent<LocalToWorld>(start).Position;
                float3 b = SystemAPI.GetComponent<LocalToWorld>(end).Position;
                float3 dir = b - a;
                float len = math.length(dir);
                if (len >= 1e-4f)
                {
                    float3 fwd = dir / len;
                    quaternion rot = quaternion.LookRotationSafe(fwd, math.up());
                    float3 mid = a + dir * 0.5f;
                    var cfg = SystemAPI.GetSingleton<WirePreviewConfig>();
                    var visual = ecb.Instantiate(cfg.Prefab);

                    ecb.AddComponent(visual, new WireVisualTag());
                    ecb.AddComponent(visual, new WireOwner { Wire = wireLogic });
                    ecb.AddComponent(visual, LocalTransform.FromPositionRotationScale(mid, rot, 1f));
                    ecb.AddComponent(visual, new PostTransformMatrix
                    {
                        Value = float4x4.Scale(new float3(cfg.Thickness, cfg.Thickness, len))
                    });

                    ecb.AddComponent(visual, new URPMaterialPropertyBaseColor { Value = new float4(1, 1, 1, 1) });
                }
            }
        }

        private Entity PickConnector(URay ray, in PhysicsWorldSingleton pws)
        {
            var cw = pws.CollisionWorld;
            if (cw.NumBodies == 0) return Entity.Null;
            var input = new RaycastInput
            {
                Start = ray.origin,
                End = ray.origin + ray.direction * 2000f,
                Filter = CollisionFilter.Default
            };
            if (!cw.CastRay(input, out var hit)) return Entity.Null;
            float3 hitPos = math.lerp(input.Start, input.End, hit.Fraction);
            var root = pws.PhysicsWorld.Bodies[hit.RigidBodyIndex].Entity;
            Entity chosen = Entity.Null;
            var keyMapRO = SystemAPI.GetBufferLookup<PhysicsColliderKeyEntityPair>(true);
            if (keyMapRO.HasBuffer(root))
            {
                var pairs = keyMapRO[root];
                for (int i = 0; i < pairs.Length; i++)
                {
                    if (pairs[i].Key.Equals(hit.ColliderKey) && SystemAPI.HasComponent<WireConnector>(pairs[i].Entity))
                    {
                        chosen = pairs[i].Entity;
                        break;
                    }
                }
            }
            if (chosen == Entity.Null)
            {
                float bestDistSq = kPickRadius * kPickRadius;
                foreach (var (ltw, entity) in SystemAPI.Query<RefRO<LocalToWorld>>().WithAll<WireConnector>().WithNone<Prefab>().WithEntityAccess())
                {
                    float d2 = math.lengthsq(ltw.ValueRO.Position - hitPos);
                    if (d2 < bestDistSq)
                    {
                        bestDistSq = d2;
                        chosen = entity;
                    }
                }
            }
            return chosen;
        }

        private Entity FindVisualForWire(Entity wireEntity)
        {
            foreach (var (owner, entity) in SystemAPI.Query<RefRO<WireOwner>>().WithAll<WireVisualTag>().WithEntityAccess())
            {
                if (owner.ValueRO.Wire == wireEntity) return entity;
            }
            return Entity.Null;
        }

        private int FindWireItemIdByLevel(ItemRegistry registry, int level)
        {
            if (registry == null) return -1;
            foreach (var item in registry.allItems)
            {
                if (item is WireItem wireItem && wireItem.wireLevel == level)
                {
                    return wireItem.itemID;
                }
            }
            return -1;
        }

        private bool HasItemInInventory(DynamicBuffer<InventoryItemElement> inventory, int itemID)
        {
            foreach (var item in inventory)
            {
                if (item.ItemID == itemID && item.Amount > 0)
                {
                    return true;
                }
            }
            return false;
        }
    }
}