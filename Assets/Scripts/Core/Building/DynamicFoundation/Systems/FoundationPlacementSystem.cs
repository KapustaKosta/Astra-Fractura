using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Transforms;
using UnityEngine; 

using URay = UnityEngine.Ray;
using PhRaycastHit = Unity.Physics.RaycastHit;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BuildingHeightAdjustmentSystem))]
[UpdateBefore(typeof(FinalizeBuildingSystem))] 
public partial class FoundationPlacementSystem : SystemBase 
{
    private const float EDGE_SNAP_MAX_DIST = 6.0f;
    private const float SIDE_NORMAL_Y_THRESHOLD = 0.35f;
    private const float DOWNCAST_EXTRA_HEIGHT = 4.0f;
    private const float OUTWARD_OFFSET = 0.35f;
    private const float EDGE_ASSIST_SPHERE_RADIUS = 0.45f;
    private const int EDGE_STICKY_FRAMES = 12;
    private const float EDGE_FAN_RADIUS_PX = 16f;

    private BlobAssetReference<Unity.Physics.Collider> _assistSphere;
    private int _stickyFramesLeft;
    private bool _hasEdgeLock;
    private float3 _lockAnchor;
    private float _lockDeckY;
    private float3 _lockNormal;
    private float2 _lockDeckCenter;
    private float2 _lockDeckSize;
    private quaternion _lockDeckOrientation;

    protected override void OnCreate()
    {
        RequireForUpdate<PhysicsWorldSingleton>();
        RequireForUpdate<BuildingPreviewTag>();
        RequireForUpdate<BuildingSettings>();
        RequireForUpdate<FoundationTag>(); // Эта система обновляется только для фундаментов

        var em = EntityManager;
        if (!SystemAPI.HasSingleton<FoundationPlacementSnapshot>())
        {
            var e = em.CreateEntity();
            em.AddComponentData(e, new FoundationPlacementSnapshot
            {
                ScaleY = 1f,
                TotalHeight = 0f,
                ExpectedPos = float3.zero,
                HasData = 0,
                HasTargetDeckY = 0,
                TargetDeckY = 0f
            });
        }

        var filter = new CollisionFilter { BelongsTo = ~0u, CollidesWith = ~0u, GroupIndex = 0 };
        var geom = new SphereGeometry { Center = float3.zero, Radius = EDGE_ASSIST_SPHERE_RADIUS };
        _assistSphere = Unity.Physics.SphereCollider.Create(geom, filter, default(Unity.Physics.Material));
    }

    protected override void OnDestroy()
    {
        if (_assistSphere.IsCreated) _assistSphere.Dispose();
    }

    protected override void OnUpdate()
    {
        if (!SystemAPI.TryGetSingletonEntity<BuildingPreviewTag>(out var previewEntity))
            return;
        
        if (!SystemAPI.HasComponent<FoundationTag>(previewEntity))
            return;

        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        var settings = SystemAPI.GetSingleton<BuildingSettings>();
        var em = EntityManager;

        var cam = Camera.main;
        if (cam == null)
        {
            SetPlacementInvalid(previewEntity, em); 
            return;
        }

        URay mainRay = cam.ScreenPointToRay(Input.mousePosition);
        var mainRayInput = new RaycastInput
        {
            Start = mainRay.origin,
            End = mainRay.origin + mainRay.direction * settings.MaxPlacementDistance,
            Filter = new CollisionFilter
            {
                BelongsTo = ~0u,
                CollidesWith = (uint)settings.BuildableSurfaceLayerMask,
                GroupIndex = 0
            }
        };

        var lt = em.GetComponentData<LocalTransform>(previewEntity);
        PhRaycastHit mainRayHit = default;
        bool initialPositionFound = physicsWorld.CollisionWorld.CastRay(mainRayInput, out mainRayHit);

        if (!initialPositionFound)
        {
            SetPlacementInvalid(previewEntity, em);
            return;
        }

        // Сначала обрабатываем логику позиционирования/привязки для фундаментов
        HandleFoundationPlacement(ref lt, previewEntity, mainRay, mainRayHit, in physicsWorld, in settings);
        em.SetComponentData(previewEntity, lt); 

        // Теперь выполняем общие проверки валидации для *конечной* трансформации превью.
        bool isPlacementValid = true; 

        if (SystemAPI.HasComponent<PhysicsCollider>(previewEntity))
        {
            var currentPreviewTransform = em.GetComponentData<LocalTransform>(previewEntity);
            var previewCollider = em.GetComponentData<PhysicsCollider>(previewEntity);
            var previewAabb = previewCollider.Value.Value.CalculateAabb(new RigidTransform(currentPreviewTransform.Rotation, currentPreviewTransform.Position));

            uint buildableSurfaceLayerMask = (uint)settings.BuildableSurfaceLayerMask;
            uint obstacleLayerMask = (uint)settings.ObstacleLayerMask; 

            // 1. Проверка на пересечение (Overlap Check)
            var overlapInput = new OverlapAabbInput
            {
                Aabb = previewAabb,
                Filter = new CollisionFilter { BelongsTo = ~0u, CollidesWith = obstacleLayerMask, GroupIndex = 0 }
            };
            var overlappingBodies = new NativeList<int>(Allocator.Temp);
            if (physicsWorld.CollisionWorld.OverlapAabb(overlapInput, ref overlappingBodies))
            {
                isPlacementValid = false;
                // Debug.Log($"<color=red>Foundation Validation FAILED ({previewEntity}): Overlap with {overlappingBodies.Length} obstacles.</color>");
            }
            overlappingBodies.Dispose();
            
        }
        else 
        {
            isPlacementValid = false; 
            // Debug.Log($"<color=red>Foundation Validation FAILED ({previewEntity}): Preview entity has no PhysicsCollider.</color>");
        }

        // Debug.Log($"<color=green>Final Validity for Foundation {previewEntity}: {isPlacementValid}</color>");
        
        // Обновляем теги валидности размещения.
        if (isPlacementValid)
        {
            SetPlacementValid(previewEntity, em);
        }
        else
        {
            SetPlacementInvalid(previewEntity, em);
        }

        // Логика Foundation Placement Snapshot для FinalizeFoundationSystem
        if (Input.GetMouseButtonDown(0))
        {
            var snap = SystemAPI.GetSingleton<FoundationPlacementSnapshot>();

            if (em.HasComponent<PlacementValidTag>(previewEntity)) 
            {
                float baseH = 1f;
                if (em.HasComponent<FoundationTileHeight>(previewEntity))
                    baseH = math.max(0.01f, em.GetComponentData<FoundationTileHeight>(previewEntity).Value);

                float scaleY = 1f;
                if (em.HasComponent<PostTransformMatrix>(previewEntity))
                {
                    var postM = em.GetComponentData<PostTransformMatrix>(previewEntity).Value;
                    scaleY = math.abs(postM.c1.y);
                    if (scaleY < 1e-4f) scaleY = 1f;
                }
                float totalH = baseH * scaleY;

                byte hasTarget = 0;
                float targetY = 0f;
                if (em.HasComponent<PreviewHeightSnapState>(previewEntity))
                {
                    var st = em.GetComponentData<PreviewHeightSnapState>(previewEntity);
                    if (st.IsActive != 0)
                    {
                        hasTarget = 1;
                        targetY = st.TargetDeckY;
                    }
                }

                snap.ScaleY = scaleY;
                snap.TotalHeight = totalH;
                snap.ExpectedPos = lt.Position;
                snap.HasData = 1;
                snap.HasTargetDeckY = hasTarget;
                snap.TargetDeckY = targetY;
                SystemAPI.SetSingleton(snap);
            }
            else
            {
                snap.HasData = 0;
                snap.HasTargetDeckY = 0;
                snap.TargetDeckY = 0f;
                SystemAPI.SetSingleton(snap);
            }
        }
    }

    private void SetPlacementValid(Entity previewEntity, EntityManager em)
    {
        if (!em.HasComponent<PlacementValidTag>(previewEntity))
            em.AddComponentData(previewEntity, new PlacementValidTag());
        if (em.HasComponent<PlacementInvalidTag>(previewEntity))
            em.RemoveComponent<PlacementInvalidTag>(previewEntity);
    }

    private void SetPlacementInvalid(Entity previewEntity, EntityManager em)
    {
        if (!em.HasComponent<PlacementInvalidTag>(previewEntity))
            em.AddComponentData(previewEntity, new PlacementInvalidTag());
        if (em.HasComponent<PlacementValidTag>(previewEntity))
            em.RemoveComponent<PlacementValidTag>(previewEntity);
    }

    private bool HandleFoundationPlacement(ref LocalTransform lt, Entity previewEntity, URay mainRay, PhRaycastHit hit, in PhysicsWorldSingleton physicsWorld, in BuildingSettings settings)
    {
        var em = EntityManager;
        var cam = Camera.main;

        float3 pivotOffset = em.HasComponent<BuildingPivotOffset>(previewEntity)
            ? em.GetComponentData<BuildingPivotOffset>(previewEntity).Value
            : float3.zero;

        bool usedEdgeDowncast = false;

        if (TryEdgeFromRayHit(ref lt, previewEntity, in hit, in physicsWorld, in settings))
            usedEdgeDowncast = true;
        else if (TryEdgeBySphereCast(ref lt, previewEntity, mainRay, in physicsWorld, in settings))
            usedEdgeDowncast = true;
        else if (TryEdgeByFanRays(ref lt, previewEntity, cam, in physicsWorld, in settings))
            usedEdgeDowncast = true;
        else
        {
            if (_hasEdgeLock && _stickyFramesLeft > 0)
            {
                _stickyFramesLeft--;

                float2 previewSize = em.HasComponent<BuildingFootprint>(previewEntity) ? em.GetComponentData<BuildingFootprint>(previewEntity).Size : new float2(4f, 4f);
                quaternion deckRot = _lockDeckOrientation;
                quaternion invRot = math.inverse(deckRot);
                float3 deckCenter3 = new float3(_lockDeckCenter.x, 0f, _lockDeckCenter.y);
                float3 prevLocal = math.mul(invRot, (new float3(lt.Position.x, 0f, lt.Position.z) - deckCenter3));
                float3 normalLocal = math.mul(invRot, new float3(_lockNormal.x, 0f, _lockNormal.z));
                float2 deckHalf = _lockDeckSize * 0.5f;
                float2 previewHalf = previewSize * 0.5f;

                float3 anchorLocal = 0f;
                if (math.abs(normalLocal.x) >= math.abs(normalLocal.z))
                {
                    float s = math.sign(normalLocal.x);
                    anchorLocal.x = s * (deckHalf.x + previewHalf.x);
                    anchorLocal.z = math.clamp(prevLocal.z, -deckHalf.y + previewHalf.y, deckHalf.y - previewHalf.y);
                }
                else
                {
                    float s = math.sign(normalLocal.z);
                    anchorLocal.z = s * (deckHalf.y + previewHalf.y);
                    anchorLocal.x = math.clamp(prevLocal.x, -deckHalf.x + previewHalf.x, deckHalf.x - previewHalf.x);
                }

                float3 anchorWorld3 = deckCenter3 + math.mul(deckRot, anchorLocal);
                float2 anchorXZ = new float2(anchorWorld3.x, anchorWorld3.z);

                float3 outward = math.normalize(new float3(_lockNormal.x, 0f, _lockNormal.z));
                float3 start = new float3(anchorXZ.x, _lockDeckY + DOWNCAST_EXTRA_HEIGHT, anchorXZ.y) + outward * OUTWARD_OFFSET;

                var downInput = new RaycastInput
                {
                    Start = start,
                    End = start + new float3(0, -2000, 0),
                    Filter = new CollisionFilter { BelongsTo = ~0u, CollidesWith = (uint)settings.BuildableSurfaceLayerMask, GroupIndex = 0 }
                };

                if (physicsWorld.CollisionWorld.CastRay(downInput, out PhRaycastHit downHit))
                {
                    float groundY = downHit.Position.y;
                    lt.Position = new float3(anchorXZ.x, groundY, anchorXZ.y) - pivotOffset;
                    lt.Rotation = deckRot;
                    if (em.HasComponent<PreviewGroundPosition>(previewEntity))
                        em.SetComponentData(previewEntity, new PreviewGroundPosition { GroundY = groundY });
                    else
                        em.AddComponentData(previewEntity, new PreviewGroundPosition { GroundY = groundY });
                    usedEdgeDowncast = true;
                }
                else
                {
                    _stickyFramesLeft = 0;
                }
            }

            if (!usedEdgeDowncast)
            {
                float3 groundPos = hit.Position;
                if (TryResolveFoundationUnderCursor(hit.Entity, em, out _, out _, out float virtualGroundY))
                    groundPos.y = virtualGroundY;

                lt.Position = groundPos - pivotOffset;
                if (!_hasEdgeLock)
                    lt.Rotation = quaternion.identity;

                if (em.HasComponent<PreviewGroundPosition>(previewEntity))
                    em.SetComponentData(previewEntity, new PreviewGroundPosition { GroundY = groundPos.y });
                else
                    em.AddComponentData(previewEntity, new PreviewGroundPosition { GroundY = groundPos.y });

                _hasEdgeLock = false;
                _stickyFramesLeft = 0;
            }
        }

        if (em.HasComponent<BuildingFootprint>(previewEntity))
        {
            float2 size = em.GetComponentData<BuildingFootprint>(previewEntity).Size;
            float2 bestXZ = new float2(lt.Position.x, lt.Position.z);
            float bestD2 = float.MaxValue;
            quaternion bestRot = lt.Rotation;

            foreach (var deckRO in SystemAPI.Query<RefRO<FoundationDeck>>())
            {
                var deck = deckRO.ValueRO;
                float2 dc2 = deck.CenterXZ;
                float2 ds2 = deck.SizeXZ;
                quaternion rot = deck.Orientation;
                quaternion invRot = math.inverse(rot);

                float3 deckCenter3 = new float3(dc2.x, 0f, dc2.y);
                float3 prevLocal = math.mul(invRot, (new float3(lt.Position.x, 0f, lt.Position.z) - deckCenter3));

                float2 deckHalf = ds2 * 0.5f;
                float2 previewHalf = size * 0.5f;

                var candidatesLocal = new NativeArray<float3>(4, Allocator.Temp);
                candidatesLocal[0] = new float3(deckHalf.x + previewHalf.x, 0f, math.clamp(prevLocal.z, -deckHalf.y + previewHalf.y, deckHalf.y - previewHalf.y));
                candidatesLocal[1] = new float3(-deckHalf.x - previewHalf.x, 0f, math.clamp(prevLocal.z, -deckHalf.y + previewHalf.y, deckHalf.y - previewHalf.y));
                candidatesLocal[2] = new float3(math.clamp(prevLocal.x, -deckHalf.x + previewHalf.x, deckHalf.x - previewHalf.x), 0f, deckHalf.y + previewHalf.y);
                candidatesLocal[3] = new float3(math.clamp(prevLocal.x, -deckHalf.x + previewHalf.x, deckHalf.x - previewHalf.x), 0f, -deckHalf.y - previewHalf.y);

                for (int i = 0; i < candidatesLocal.Length; i++)
                {
                    float3 candidateWorld3 = deckCenter3 + math.mul(rot, candidatesLocal[i]);
                    float2 candidateXZ = new float2(candidateWorld3.x, candidateWorld3.z);
                    float d2 = math.distancesq(candidateXZ, new float2(lt.Position.x, lt.Position.z));
                    if (d2 < bestD2)
                    {
                        bestD2 = d2;
                        bestXZ = candidateXZ;
                        bestRot = rot;
                    }
                }
                candidatesLocal.Dispose();
            }

            if (bestD2 <= EDGE_SNAP_MAX_DIST * EDGE_SNAP_MAX_DIST)
            {
                lt.Position.x = bestXZ.x;
                lt.Position.z = bestXZ.y;
                lt.Rotation = bestRot;
            }
        }
        return true; 
    }

    private bool TryResolveFoundationRoot(Entity hitEntity, EntityManager em, out Entity root, out FoundationDeck deck)
    {
        root = hitEntity;
        deck = default;
        int hops = 0;
        const int MAX_HOPS = 8;
        while (root != Entity.Null && !em.HasComponent<FoundationDeck>(root) && hops < MAX_HOPS)
        {
            if (em.HasComponent<Parent>(root))
                root = em.GetComponentData<Parent>(root).Value;
            else
                break;
            hops++;
        }
        if (root == Entity.Null || !em.HasComponent<FoundationDeck>(root))
            return false;
        deck = em.GetComponentData<FoundationDeck>(root);
        return true;
    }

    private bool ApplyEdgeDowncast(ref LocalTransform lt, Entity previewEntity, float3 worldHitPos, float3 normal, in FoundationDeck deck, in PhysicsWorldSingleton physicsWorld, in BuildingSettings settings)
    {
        var em = EntityManager;
        float2 previewSize = em.HasComponent<BuildingFootprint>(previewEntity) ? em.GetComponentData<BuildingFootprint>(previewEntity).Size : new float2(4f, 4f);
        float3 pivotOffset = em.HasComponent<BuildingPivotOffset>(previewEntity) ? em.GetComponentData<BuildingPivotOffset>(previewEntity).Value : float3.zero;

        float2 deckCenterXZ = deck.CenterXZ;
        float2 deckSize = deck.SizeXZ;
        float deckWorldY = deck.DeckWorldY;
        quaternion deckRot = deck.Orientation;

        quaternion invRot = math.inverse(deckRot);
        float3 deckCenter3 = new float3(deckCenterXZ.x, 0f, deckCenterXZ.y);
        float3 hitLocal = math.mul(invRot, (new float3(worldHitPos.x, 0f, worldHitPos.z) - deckCenter3));
        float3 normalLocal = math.mul(invRot, new float3(normal.x, 0f, normal.z));
        float2 deckHalf = deckSize * 0.5f;
        float2 previewHalf = previewSize * 0.5f;

        float3 anchorLocal = 0f;
        if (math.abs(normalLocal.x) >= math.abs(normalLocal.z))
        {
            float s = math.sign(normalLocal.x);
            anchorLocal.x = s * (deckHalf.x + previewHalf.x);
            anchorLocal.z = math.clamp(hitLocal.z, -deckHalf.y + previewHalf.y, deckHalf.y - previewHalf.y);
        }
        else
        {
            float s = math.sign(normalLocal.z);
            anchorLocal.z = s * (deckHalf.y + previewHalf.y);
            anchorLocal.x = math.clamp(hitLocal.x, -deckHalf.x + previewHalf.x, deckHalf.x - previewHalf.x);
        }

        float3 anchorWorld3 = deckCenter3 + math.mul(deckRot, anchorLocal);
        float2 anchorXZ = new float2(anchorWorld3.x, anchorWorld3.z);
        float3 outward = math.normalize(new float3(normal.x, 0f, normal.z));
        float3 start = new float3(anchorXZ.x, deckWorldY + DOWNCAST_EXTRA_HEIGHT, anchorXZ.y) + outward * OUTWARD_OFFSET;

        var downInput = new RaycastInput
        {
            Start = start,
            End = start + new float3(0f, -2000f, 0f),
            Filter = new CollisionFilter { BelongsTo = ~0u, CollidesWith = (uint)settings.BuildableSurfaceLayerMask, GroupIndex = 0 }
        };

        if (physicsWorld.CollisionWorld.CastRay(downInput, out PhRaycastHit downHit))
        {
            float groundY = downHit.Position.y;
            lt.Position = new float3(anchorXZ.x, groundY, anchorXZ.y) - pivotOffset;
            lt.Rotation = deckRot;

            if (em.HasComponent<PreviewGroundPosition>(previewEntity))
                em.SetComponentData(previewEntity, new PreviewGroundPosition { GroundY = groundY });
            else
                em.AddComponentData(previewEntity, new PreviewGroundPosition { GroundY = groundY });

            _hasEdgeLock = true;
            _stickyFramesLeft = EDGE_STICKY_FRAMES;
            _lockAnchor = new float3(anchorXZ.x, groundY, anchorXZ.y);
            _lockDeckY = deckWorldY;
            _lockNormal = normal;
            _lockDeckCenter = deckCenterXZ;
            _lockDeckSize = deckSize;
            _lockDeckOrientation = deckRot;
            return true;
        }
        return false;
    }

    private bool TryEdgeFromRayHit(ref LocalTransform lt, Entity previewEntity, in PhRaycastHit h, in PhysicsWorldSingleton physicsWorld, in BuildingSettings settings)
    {
        if (math.abs(h.SurfaceNormal.y) >= SIDE_NORMAL_Y_THRESHOLD) return false;
        if (!TryResolveFoundationRoot(h.Entity, EntityManager, out _, out var deck)) return false;
        return ApplyEdgeDowncast(ref lt, previewEntity, h.Position, h.SurfaceNormal, deck, in physicsWorld, in settings);
    }

    private bool TryEdgeBySphereCast(ref LocalTransform lt, Entity previewEntity, URay ray, in PhysicsWorldSingleton physicsWorld, in BuildingSettings settings)
    {
        if (!_assistSphere.IsCreated) return false;
        var castInput = new ColliderCastInput
        {
            Start = ray.origin,
            End = ray.origin + ray.direction * settings.MaxPlacementDistance,
            Orientation = quaternion.identity
        };
        castInput.SetCollider(_assistSphere);
        if (!physicsWorld.CollisionWorld.CastCollider(castInput, out ColliderCastHit ch)) return false;
        if (math.abs(ch.SurfaceNormal.y) >= SIDE_NORMAL_Y_THRESHOLD) return false;
        if (!TryResolveFoundationRoot(ch.Entity, EntityManager, out _, out var deck)) return false;
        return ApplyEdgeDowncast(ref lt, previewEntity, ch.Position, ch.SurfaceNormal, deck, in physicsWorld, in settings);
    }

    private bool TryEdgeByFanRays(ref LocalTransform lt, Entity previewEntity, Camera c, in PhysicsWorldSingleton physicsWorld, in BuildingSettings settings)
    {
        var offs = new NativeArray<Vector2>(8, Allocator.Temp)
        {
            [0] = new Vector2(EDGE_FAN_RADIUS_PX, 0),
            [1] = new Vector2(-EDGE_FAN_RADIUS_PX, 0),
            [2] = new Vector2(0, EDGE_FAN_RADIUS_PX),
            [3] = new Vector2(0, -EDGE_FAN_RADIUS_PX),
            [4] = new Vector2(EDGE_FAN_RADIUS_PX * 0.707f, EDGE_FAN_RADIUS_PX * 0.707f),
            [5] = new Vector2(-EDGE_FAN_RADIUS_PX * 0.707f, EDGE_FAN_RADIUS_PX * 0.707f),
            [6] = new Vector2(EDGE_FAN_RADIUS_PX * 0.707f, -EDGE_FAN_RADIUS_PX * 0.707f),
            [7] = new Vector2(-EDGE_FAN_RADIUS_PX * 0.707f, -EDGE_FAN_RADIUS_PX * 0.707f)
        };
        Vector2 basePos = Input.mousePosition;
        for (int i = 0; i < offs.Length; i++)
        {
            URay r = c.ScreenPointToRay(basePos + offs[i]);
            var input = new RaycastInput
            {
                Start = r.origin,
                End = r.origin + r.direction * settings.MaxPlacementDistance,
                Filter = new CollisionFilter { BelongsTo = ~0u, CollidesWith = (uint)settings.BuildableSurfaceLayerMask, GroupIndex = 0 }
            };
            if (physicsWorld.CollisionWorld.CastRay(input, out PhRaycastHit h))
            {
                if (TryEdgeFromRayHit(ref lt, previewEntity, in h, in physicsWorld, in settings))
                {
                    offs.Dispose();
                    return true;
                }
            }
        }
        offs.Dispose();
        return false;
    }

    private static bool TryResolveFoundationUnderCursor(Entity hitEntity, EntityManager em, out Entity root, out FoundationDeck deck, out float virtualGroundY)
    {
        virtualGroundY = 0f;
        root = hitEntity;
        deck = default;
        int hops = 0;
        const int MAX_HOPS = 8;
        while (root != Entity.Null && !em.HasComponent<FoundationDeck>(root) && hops < MAX_HOPS)
        {
            if (em.HasComponent<Parent>(root))
                root = em.GetComponentData<Parent>(root).Value;
            else
                break;
            hops++;
        }
        if (root == Entity.Null || !em.HasComponent<FoundationDeck>(root))
            return false;
        deck = em.GetComponentData<FoundationDeck>(root);
        float baseY = 0f;
        if (em.HasComponent<LocalTransform>(root) && em.HasComponent<BuildingPivotOffset>(root))
        {
            var ltr = em.GetComponentData<LocalTransform>(root);
            var pivot = em.GetComponentData<BuildingPivotOffset>(root).Value;
            baseY = ltr.Position.y + pivot.y;
        }
        else
        {
            baseY = deck.DeckWorldY;
        }
        float deckHeight = math.max(0.001f, deck.DeckWorldY - baseY);
        virtualGroundY = deck.DeckWorldY - deckHeight;
        return true;
    }
}