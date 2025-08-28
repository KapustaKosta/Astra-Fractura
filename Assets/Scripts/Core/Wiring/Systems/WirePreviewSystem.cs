using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using UnityEngine;
using Unity.Rendering;
using Unity.Collections;
using Plane = UnityEngine.Plane;

namespace Wiring
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class WirePreviewSystem : SystemBase
    {
        private EndSimulationEntityCommandBufferSystem _endSimEcb;
        private EntityQuery _previewQuery;

        protected override void OnCreate()
        {
            _endSimEcb = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
            _previewQuery = GetEntityQuery(ComponentType.ReadOnly<WirePreviewTag>());
        }

        protected override void OnUpdate()
        {
            var ecb = _endSimEcb.CreateCommandBuffer();

            bool inWire = SystemAPI.HasSingleton<InWirePlacementMode>();
            bool hasPending = SystemAPI.TryGetSingletonEntity<PendingWire>(out var pendingE);
            int previewCount = _previewQuery.CalculateEntityCount();

            // Вне режима / нет pending — чистим превью и выходим
            if (!inWire || !hasPending)
            {
                if (previewCount > 0)
                    ecb.DestroyEntity(_previewQuery, EntityQueryCaptureMode.AtPlayback);
                return;
            }

            var pending = SystemAPI.GetComponent<PendingWire>(pendingE);
            if (!SystemAPI.HasComponent<LocalToWorld>(pending.StartConnector))
            {
                if (previewCount > 0)
                    ecb.DestroyEntity(_previewQuery, EntityQueryCaptureMode.AtPlayback);
                return;
            }

            float3 a = SystemAPI.GetComponent<LocalToWorld>(pending.StartConnector).Position;
            float3 b = a;

            // Берём точку наведения мыши (raycast → иначе — пересечение с плоскостью через A)
            var cam = Camera.main;
            if (cam != null)
            {
                var ray = cam.ScreenPointToRay(Input.mousePosition);
                if (SystemAPI.TryGetSingleton(out PhysicsWorldSingleton pws))
                {
                    var input = new RaycastInput
                    {
                        Start = ray.origin,
                        End = ray.origin + ray.direction * 5000f,
                        Filter = CollisionFilter.Default
                    };
                    if (pws.CollisionWorld.CastRay(input, out var hit))
                        b = hit.Position;
                    else
                    {
                        var plane = new Plane(Vector3.up, a);
                        if (plane.Raycast(ray, out float enter)) b = ray.GetPoint(enter);
                    }
                }
                else
                {
                    var plane = new Plane(Vector3.up, a);
                    if (plane.Raycast(ray, out float enter)) b = ray.GetPoint(enter);
                }
            }

            float3 dir = b - a;
            float len = math.length(dir);
            if (len < 0.1f)
            {
                if (previewCount > 0)
                    ecb.DestroyEntity(_previewQuery, EntityQueryCaptureMode.AtPlayback);
                return;
            }

            float3 fwd = dir / (len + 1e-5f);
            float3 mid = a + dir * 0.5f;

            // Как в WireVisualizationSystem: без +90 по X, длина по Z
            quaternion rot = quaternion.LookRotationSafe(fwd, math.up());

            if (!SystemAPI.TryGetSingleton(out WirePreviewConfig cfg))
            {
                if (previewCount > 0)
                    ecb.DestroyEntity(_previewQuery, EntityQueryCaptureMode.AtPlayback);
                return;
            }

            // Масштаб в точности как в финальном визуале:
            // толщина по X/Y, длина по Z.
            float3 scaleVector = new float3(cfg.Thickness, cfg.Thickness, len);

            // Цвет по доступности ресурсов (как было)
            bool canAfford = false;
            var itemRegistry = ItemRegistry.Instance;
            if (itemRegistry != null && SystemAPI.TryGetSingletonEntity<PlayerTag>(out var playerEntity))
            {
                if (SystemAPI.HasBuffer<InventoryItemElement>(playerEntity))
                {
                    var inventory = SystemAPI.GetBuffer<InventoryItemElement>(playerEntity);
                    int wireItemId = -1;
                    var all = itemRegistry.allItems;
                    for (int i = 0; i < all.Count; i++)
                        if (all[i] is WireItem w && w.wireLevel == pending.Level) { wireItemId = w.itemID; break; }
                    if (wireItemId != -1)
                    {
                        int totalAmount = 0;
                        for (int i = 0; i < inventory.Length; i++)
                            if (inventory[i].ItemID == wireItemId) totalAmount += inventory[i].Amount;
                        canAfford = totalAmount >= (int)math.ceil(len);
                    }
                }
            }
            float4 previewColor = canAfford ? new float4(0.2f, 1f, 0.2f, 0.5f)
                                            : new float4(1f, 0.2f, 0.2f, 0.5f);

            if (previewCount == 0)
            {
                var previewE = ecb.Instantiate(cfg.Prefab);
                ecb.AddComponent<WirePreviewTag>(previewE);
                ecb.SetComponent(previewE, LocalTransform.FromPositionRotationScale(mid, rot, 1f));
                ecb.AddComponent(previewE, new PostTransformMatrix { Value = float4x4.Scale(scaleVector) });
                ecb.AddComponent(previewE, new URPMaterialPropertyBaseColor { Value = previewColor });
            }
            else
            {
                Entity previewE;
                using (var arr = _previewQuery.ToEntityArray(Allocator.Temp))
                    previewE = arr[0];

                ecb.SetComponent(previewE, LocalTransform.FromPositionRotationScale(mid, rot, 1f));
                ecb.SetComponent(previewE, new PostTransformMatrix { Value = float4x4.Scale(scaleVector) });

                if (SystemAPI.HasComponent<URPMaterialPropertyBaseColor>(previewE))
                    ecb.SetComponent(previewE, new URPMaterialPropertyBaseColor { Value = previewColor });
                else
                    ecb.AddComponent(previewE, new URPMaterialPropertyBaseColor { Value = previewColor });
            }

            _endSimEcb.AddJobHandleForProducer(Dependency);
        }
    }
}
