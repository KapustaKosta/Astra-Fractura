using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using UnityEngine;

namespace Conveyor
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ConfirmConveyorPlacementSystem))]
    public partial class ConveyorFinalizeSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<GameState>();
            RequireForUpdate<PlayerTag>();
        }

        protected override void OnUpdate()
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                               .CreateCommandBuffer(World.Unmanaged);
            var em = EntityManager;
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;

            if (!SystemAPI.TryGetSingletonEntity<GameState>(out var gs)) return;
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();

            foreach (var (req, path, entity) in SystemAPI
                         .Query<RefRO<ConveyorBuildFromPathRequest>, DynamicBuffer<ConveyorBuildPathPoint>>()
                         .WithEntityAccess())
            {
                var request = req.ValueRO;
                if (request.PreviewHolder == Entity.Null || !em.Exists(request.PreviewHolder) || path.Length < 2)
                {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                var prefab = ItemToEntityResolver.GetEntityPrefabFromID(em, request.ItemID);
                if (prefab == Entity.Null) { ecb.DestroyEntity(entity); continue; }

                // Проверяем, нужен ли опорный фундамент для конвейера
                bool needsFoundation = SystemAPI.HasComponent<RequiresDynamicFoundation>(prefab);
                Entity pillarPrefab = needsFoundation
                    ? SystemAPI.GetComponent<RequiresDynamicFoundation>(prefab).FoundationPrefab
                    : Entity.Null;

                float baseLen = 6f, minLen = 1f, maxLen = 8.1f;
                if (em.HasComponent<ConveyorSegmentSettings>(prefab))
                {
                    var s = em.GetComponentData<ConveyorSegmentSettings>(prefab);
                    baseLen = s.Length > 0 ? s.Length : baseLen;
                    minLen = s.MinLength > 0 ? math.max(1f, s.MinLength) : minLen;
                    maxLen = s.MaxLength > 0 ? math.max(minLen, s.MaxLength) : maxLen;
                }
                minLen = math.clamp(minLen, 0.01f, maxLen - 1e-4f);

                // Пост-обновление коннекторов
                var postBuildReq = ecb.CreateEntity();
                ecb.AddComponent(postBuildReq, new PostBuildConnectorUpdateRequest
                {
                    StartConnector = request.StartConnector,
                    EndConnector = request.EndConnector
                });
                var newSegs = ecb.AddBuffer<NewlyBuiltConveyorSegmentRef>(postBuildReq);

                int total = 0;

                for (int i = 0; i < path.Length - 1; i++)
                {
                    float3 a = path[i].Position;
                    float3 b = path[i + 1].Position;

                    // Строим по XZ, длину считаем по проекции
                    float3 dir = b - a; dir.y = 0;
                    float lenXZ = math.length(new float2(dir.x, dir.z));
                    if (lenXZ < math.max(1e-4f, 0.5f * minLen)) continue;

                    var forward = math.normalizesafe(new float3(dir.x, 0, dir.z));
                    var rot = quaternion.LookRotationSafe(forward, math.up());

                    ConveyorQuantization.QuantizeStraight(lenXZ, minLen, maxLen, out int cnt, out float per);
                    total += cnt;

                    // Высота сегмента: берём гарантированно не ниже обоих концов
                    // (если точки пути уже на одной высоте — сохранится ровная линия)
                    float segmentY = math.max(a.y, b.y);

                    for (int k = 0; k < cnt; k++)
                    {
                        var inst = ecb.Instantiate(prefab);

                        float d = per * (k + 0.5f);
                        float t = math.saturate(d / math.max(lenXZ, 1e-4f));
                        float3 p = math.lerp(a, b, t);
                        p.y = segmentY;

                        ecb.SetComponent(inst, LocalTransform.FromPositionRotation(p, rot));

                        float zScale = baseLen > 1e-4f ? per / baseLen : 1f;
                        ecb.AddComponent(inst, new PostTransformMatrix
                        {
                            Value = float4x4.Scale(new float3(1, 1, math.max(1e-4f, zScale)))
                        });
                        ecb.AddComponent(inst, new ConveyorSegmentScale { Z = zScale });

                        // Опора/фундамент под сегмент (по желанию префаба)
                        if (needsFoundation && pillarPrefab != Entity.Null)
                        {
                            var rayInput = new RaycastInput
                            {
                                Start = p + new float3(0, 0.1f, 0),
                                End = p - new float3(0, 200f, 0),
                                Filter = CollisionFilter.Default
                            };

                            if (physicsWorld.CastRay(rayInput, out var hit))
                            {
                                float pillarHeight = p.y - hit.Position.y;
                                if (pillarHeight > 0.1f)
                                {
                                    var pillarEntity = ecb.Instantiate(pillarPrefab);
                                    var pillarPosition = new float3(p.x, hit.Position.y, p.z);

                                    ecb.SetComponent(pillarEntity, LocalTransform.FromPosition(pillarPosition));
                                    ecb.AddComponent(pillarEntity, new PostTransformMatrix
                                    {
                                        Value = float4x4.Scale(new float3(1f, pillarHeight, 1f))
                                    });

                                    ecb.AppendToBuffer(inst, new LinkedEntityGroup { Value = pillarEntity });
                                }
                            }
                        }

                        newSegs.Add(new NewlyBuiltConveyorSegmentRef { Value = inst });
                    }
                }

                if (total > 0)
                {
                    var removeItemReq = ecb.CreateEntity();
                    ecb.AddComponent(removeItemReq, new RemoveItemRequest
                    {
                        TargetInventoryOwner = playerEntity,
                        ItemID = request.ItemID,
                        Amount = total
                    });
                }

                if (em.Exists(request.PreviewHolder) && em.HasBuffer<ConveyorPathPoint>(request.PreviewHolder))
                    em.GetBuffer<ConveyorPathPoint>(request.PreviewHolder).Clear();

                // Сброс временного состояния пост-строительства (без IsHeightLocked)
                if (em.HasComponent<ConveyorState>(gs))
                {
                    var conveyorState = em.GetComponentData<ConveyorState>(gs);
                    conveyorState.HasStart = false;
                    conveyorState.StartConnector = Entity.Null;
                    conveyorState.SegmentsLocked = 0;
                    ecb.SetComponent(gs, conveyorState);
                }

                ecb.DestroyEntity(entity);
            }
        }
    }
}
