using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using URay = UnityEngine.Ray;

namespace Conveyor
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ConveyorPreviewLifecycleSystem))]
    public partial class ConveyorPlacementInteractionSystem : SystemBase
    {
        private const int GroundUnityLayer = 3;
        private const int ConnectorUnityLayer = 12;
        private const uint ConnectorCategoryBit = 1u << ConnectorUnityLayer;
        private const uint GroundCategoryBit = 1u << GroundUnityLayer;

        private static readonly CollisionFilter kConnectorOnlyFilter = new CollisionFilter
        {
            BelongsTo = ~0u,
            CollidesWith = ConnectorCategoryBit
        };

        private static readonly CollisionFilter kGroundOnlyFilter = new CollisionFilter
        {
            BelongsTo = ~0u,
            CollidesWith = GroundCategoryBit
        };

        private EndSimulationEntityCommandBufferSystem.Singleton _endSimEcb;
        private const float kFallbackPickRadius = 0.5f; // Радиус для поиска ближайшего коннектора, если точный поиск не удался

        protected override void OnCreate()
        {
            _endSimEcb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            RequireForUpdate<InConveyorMode>();
            RequireForUpdate<ConveyorState>();
            RequireForUpdate<PhysicsWorldSingleton>();
        }

        protected override void OnUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var physics = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            URay ray = cam.ScreenPointToRay(Input.mousePosition);


            Entity newHoveredConnector = TryPickConnector(ray, ref physics);
            Entity oldHoveredConnector = Entity.Null;
            if (SystemAPI.TryGetSingletonEntity<HoveredConnectorTag>(out var oldHovered))
            {
                oldHoveredConnector = oldHovered;
            }

            if (newHoveredConnector != oldHoveredConnector)
            {
                if (EntityManager.Exists(oldHoveredConnector))
                {
                    EntityManager.RemoveComponent<HoveredConnectorTag>(oldHoveredConnector);
                }
                if (EntityManager.Exists(newHoveredConnector))
                {
                    EntityManager.AddComponent<HoveredConnectorTag>(newHoveredConnector);
                }
            }


            var stRW = SystemAPI.GetSingletonRW<ConveyorState>();
            var preview = stRW.ValueRO.PreviewEntity;
            if (preview == Entity.Null || !EntityManager.Exists(preview) || !EntityManager.HasBuffer<ConveyorPathPoint>(preview))
                return;

            var ecb = _endSimEcb.CreateCommandBuffer(World.Unmanaged);
            var pts = EntityManager.GetBuffer<ConveyorPathPoint>(preview);

            bool lmbDown = Input.GetMouseButtonDown(0);
            bool rmbDown = Input.GetMouseButtonDown(1);

            bool hasCursorOnGround = TryGetCursorOnGround(ray, ref physics, out float3 cursorOnGroundPos);

            if (rmbDown)
                HandleRMB(ecb, ref stRW, ref pts);

            if (lmbDown)
            {
                HandleLMB(ecb, ref stRW, ref pts, cursorOnGroundPos, newHoveredConnector, hasCursorOnGround, ref physics);
            }

            if (stRW.ValueRO.HasStart && hasCursorOnGround)
            {
                EnsureLiveTailExists(ref stRW, ref pts, cursorOnGroundPos, ref physics);

                if (pts.Length > 0 && pts[pts.Length - 1].IsLocked == 0)
                {
                    var last = pts[pts.Length - 1];
                    last.Position = cursorOnGroundPos;
                    pts[pts.Length - 1] = last;
                }
            }
        }

        private Entity TryPickConnector(URay ray, ref PhysicsWorldSingleton physics)
        {
            var input = new RaycastInput
            {
                Start = ray.origin,
                End = ray.origin + ray.direction * 5000f,
                Filter = kConnectorOnlyFilter
            };

            if (!physics.CollisionWorld.CastRay(input, out var hit))
                return Entity.Null;

            var rootEntity = physics.PhysicsWorld.Bodies[hit.RigidBodyIndex].Entity;
            Entity chosenConnector = Entity.Null;

            var keyMapLookup = SystemAPI.GetBufferLookup<PhysicsColliderKeyEntityPair>(true);
            if (keyMapLookup.HasBuffer(rootEntity))
            {
                var pairs = keyMapLookup[rootEntity];
                for (int i = 0; i < pairs.Length; i++)
                {
                    if (pairs[i].Key.Equals(hit.ColliderKey) && SystemAPI.HasComponent<ConveyorConnector>(pairs[i].Entity))
                    {
                        chosenConnector = pairs[i].Entity;
                        break;
                    }
                }
            }

            if (chosenConnector == Entity.Null)
            {
                float bestDistSq = kFallbackPickRadius * kFallbackPickRadius;
                foreach (var (ltw, entity) in SystemAPI.Query<RefRO<LocalToWorld>>()
                             .WithAll<ConveyorConnector>()
                             .WithEntityAccess())
                {
                    float d2 = math.lengthsq(ltw.ValueRO.Position - hit.Position);
                    if (d2 < bestDistSq)
                    {
                        bestDistSq = d2;
                        chosenConnector = entity;
                    }
                }
            }

            return chosenConnector;
        }


        private void HandleLMB(
            EntityCommandBuffer ecb,
            ref RefRW<ConveyorState> stRW,
            ref DynamicBuffer<ConveyorPathPoint> pts,
            float3 cursorPos,
            Entity pickedConnector,
            bool hasCursor,
            ref PhysicsWorldSingleton physics)
        {

            bool isPlacementValid = SystemAPI.HasComponent<ConveyorPlacementValidTag>(stRW.ValueRO.PreviewEntity);


            if (!stRW.ValueRO.HasStart)
            {
                // Начинаем строить
                if (pickedConnector != Entity.Null && IsConnectorStrictInOrOut(pickedConnector, out _))
                {
                    stRW.ValueRW.HasStart = true;
                    stRW.ValueRW.StartConnector = pickedConnector;
                    stRW.ValueRW.SegmentsLocked = 0;

                    pts.Clear();
                    var startPos = GetProjectedConnectorPositionOnGround(pickedConnector, ref physics);
                    pts.Add(new ConveyorPathPoint { Position = startPos, IsLocked = 1 });
                }
                return;
            }

            if (!isPlacementValid) return; // Если невалидно - выходим, ничего не строим.


            // Завершаем строить, подключившись к другому коннектору
            if (pickedConnector != Entity.Null &&
                pickedConnector != stRW.ValueRO.StartConnector &&
                SystemAPI.HasComponent<ConveyorConnectorHighlighted>(pickedConnector))
            {
                if (pts.Length > 0 && pts[^1].IsLocked == 0)
                    pts.RemoveAt(pts.Length - 1);

                var endPos = GetProjectedConnectorPositionOnGround(pickedConnector, ref physics);
                pts.Add(new ConveyorPathPoint { Position = endPos, IsLocked = 1 });
                CreateBuildRequestFromPreview(ecb, stRW.ValueRO, pickedConnector);
                return;
            }

            // Фиксируем промежуточную точку
            if (hasCursor && pts.Length > 0)
            {
                if (pts[^1].IsLocked == 0)
                    pts[^1] = new ConveyorPathPoint { Position = cursorPos, IsLocked = 1 };
                else
                    pts.Add(new ConveyorPathPoint { Position = cursorPos, IsLocked = 1 });

                stRW.ValueRW.SegmentsLocked++;
            }
        }

        private void HandleRMB(EntityCommandBuffer ecb, ref RefRW<ConveyorState> stRW, ref DynamicBuffer<ConveyorPathPoint> pts)
        {
            if (!stRW.ValueRO.HasStart)
            {
                ecb.AddComponent<ExitConveyorModeRequest>(ecb.CreateEntity());
                return;
            }

            if (pts.Length > 0 && pts[^1].IsLocked == 0)
                pts.RemoveAt(pts.Length - 1);

            if (pts.Length > 1 && stRW.ValueRO.SegmentsLocked > 0)
            {
                pts.RemoveAt(pts.Length - 1);
                stRW.ValueRW.SegmentsLocked--;
            }
            else
            {
                stRW.ValueRW.HasStart = false;
                stRW.ValueRW.StartConnector = Entity.Null;
                stRW.ValueRW.SegmentsLocked = 0;
                pts.Clear();
            }
        }

        private float3 GetProjectedConnectorPositionOnGround(Entity connector, ref PhysicsWorldSingleton physics)
        {
            if (!SystemAPI.HasComponent<LocalToWorld>(connector))
                return float3.zero;

            var originalPos = SystemAPI.GetComponent<LocalToWorld>(connector).Position;

            var rayInput = new RaycastInput
            {
                Start = originalPos + new float3(0, 50f, 0),
                End = originalPos - new float3(0, 100f, 0),
                Filter = kGroundOnlyFilter
            };

            if (physics.CollisionWorld.CastRay(rayInput, out var hit))
                return hit.Position;

            return new float3(originalPos.x, 0, originalPos.z);
        }

        private bool TryGetCursorOnGround(URay ray, ref PhysicsWorldSingleton physics, out float3 position)
        {
            var groundInput = new RaycastInput
            {
                Start = ray.origin,
                End = ray.origin + ray.direction * 5000f,
                Filter = kGroundOnlyFilter
            };

            if (physics.CollisionWorld.CastRay(groundInput, out var groundHit))
            {
                position = groundHit.Position;
                return true;
            }

            if (math.abs(ray.direction.y) < 1e-6f)
            {
                position = default;
                return false;
            }

            float t = -ray.origin.y / ray.direction.y;
            position = ray.origin + ray.direction * t;
            return true;
        }

        private bool IsConnectorStrictInOrOut(Entity c, out ConveyorConnectorType t)
        {
            t = ConveyorConnectorType.Bidirectional;
            if (!SystemAPI.HasComponent<ConveyorConnector>(c)) return false;
            t = SystemAPI.GetComponent<ConveyorConnector>(c).Type;
            return (t == ConveyorConnectorType.In) || (t == ConveyorConnectorType.Out);
        }

        private void EnsureLiveTailExists(
            ref RefRW<ConveyorState> stRW,
            ref DynamicBuffer<ConveyorPathPoint> pts,
            float3 cursorPos,
            ref PhysicsWorldSingleton physics)
        {
            if (!stRW.ValueRO.HasStart)
                return;

            if (pts.Length == 0)
            {
                pts.Add(new ConveyorPathPoint
                {
                    Position = GetProjectedConnectorPositionOnGround(stRW.ValueRO.StartConnector, ref physics),
                    IsLocked = 1
                });
            }

            if (pts[^1].IsLocked == 1)
            {
                pts.Add(new ConveyorPathPoint { Position = cursorPos, IsLocked = 0 });
            }
        }

        private void CreateBuildRequestFromPreview(EntityCommandBuffer ecb, in ConveyorState st, Entity endConnector)
        {
            var req = ecb.CreateEntity();
            ecb.AddComponent(req, new ConfirmConveyorPlacementRequest
            {
                ItemID = st.ItemID,
                PreviewHolder = st.PreviewEntity,
                StartConnector = st.StartConnector, // Передаем стартовый коннектор
                EndConnector = endConnector         // Передаем конечный коннектор
            });
        }
    }
}