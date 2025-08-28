using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;

namespace Wiring
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class WireVisualizationSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate(GetEntityQuery(
                ComponentType.ReadOnly<WireOwner>(),
                ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.ReadWrite<PostTransformMatrix>()));
        }

        protected override void OnUpdate()
        {
            var wireLookup = SystemAPI.GetComponentLookup<Wire>(true);
            var ltwLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);

            var thickness = SystemAPI.HasSingleton<WirePreviewConfig>()
                ? SystemAPI.GetSingleton<WirePreviewConfig>().Thickness
                : 0.03f;

            foreach (var (lt, post, owner, entity) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRW<PostTransformMatrix>, RefRO<WireOwner>>()
                              .WithEntityAccess())
            {
                var wireE = owner.ValueRO.Wire;
                if (!wireLookup.HasComponent(wireE)) continue;

                var wire = wireLookup[wireE];
                if (!ltwLookup.HasComponent(wire.StartConnector) || !ltwLookup.HasComponent(wire.EndConnector)) continue;

                float3 a = ltwLookup[wire.StartConnector].Position;
                float3 b = ltwLookup[wire.EndConnector].Position;
                float3 dir = b - a;
                float len = math.length(dir);
                if (len < 1e-4f) continue;

                float3 fwd = dir / len;
                var rot = quaternion.LookRotationSafe(fwd, math.up());
                var mid = a + dir * 0.5f;

                lt.ValueRW = LocalTransform.FromPositionRotationScale(mid, rot, 1f);
                post.ValueRW = new PostTransformMatrix { Value = float4x4.Scale(new float3(thickness, thickness, len)) };

                var half = new float3(thickness * 0.5f, thickness * 0.5f, len * 0.5f);
                if (SystemAPI.HasComponent<RenderBounds>(entity))
                    SystemAPI.SetComponent(entity, new RenderBounds { Value = new AABB { Center = float3.zero, Extents = half } });
            }
        }
    }
}
