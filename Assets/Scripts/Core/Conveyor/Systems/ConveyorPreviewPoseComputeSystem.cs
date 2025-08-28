using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Conveyor
{
    /// <summary>
    /// ФАЗА 1: читает путь и считает позы для frozen/live.
    /// Никаких Instantiate / Add/RemoveComponent — только запись в буферы поз.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ConveyorPreviewLifecycleSystem))]
    [UpdateAfter(typeof(ConveyorPolylineInputSystem))]
    public partial class ConveyorPreviewPoseComputeSystem : SystemBase
    {
        private int _cachedItemId = -1;
        private Entity _cachedPrefab = Entity.Null;

        protected override void OnCreate()
        {
            RequireForUpdate<GameState>();
        }

        protected override void OnUpdate()
        {
            var em = EntityManager;
            if (!SystemAPI.TryGetSingletonEntity<GameState>(out var gs)) return;
            if (!em.HasComponent<InConveyorMode>(gs)) return;
            if (!em.HasComponent<ConveyorState>(gs)) return;

            var st = em.GetComponentData<ConveyorState>(gs);
            var holder = st.PreviewEntity;
            if (holder == Entity.Null || !em.Exists(holder)) return;
            if (!em.HasBuffer<ConveyorPathPoint>(holder)) return;

            var path = em.GetBuffer<ConveyorPathPoint>(holder).ToNativeArray(Allocator.Temp);
            var frozenOut = em.GetBuffer<ConveyorFrozenPose>(holder);
            var liveOut = em.GetBuffer<ConveyorLivePose>(holder);

            frozenOut.Clear();
            liveOut.Clear();

            if (path.Length < 2) { path.Dispose(); return; }

            // Префаб/настройки
            if (_cachedItemId != st.ItemID || _cachedPrefab == Entity.Null || !em.Exists(_cachedPrefab))
            {
                _cachedPrefab = ItemToEntityResolver.GetEntityPrefabFromID(em, st.ItemID);
                _cachedItemId = st.ItemID;
            }
            var prefab = _cachedPrefab;
            float baseLen = 6f, minLen = 1f, maxLen = 8.1f;
            if (prefab != Entity.Null && em.HasComponent<ConveyorSegmentSettings>(prefab))
            {
                var s = em.GetComponentData<ConveyorSegmentSettings>(prefab);
                if (s.Length > 0) baseLen = s.Length;
                if (s.MinLength > 0) minLen = math.max(1f, s.MinLength);
                if (s.MaxLength > 0) maxLen = math.min(8.1f, s.MaxLength);
            }
            minLen = math.clamp(minLen, 0.01f, maxLen - 1e-4f);

            int segCount = path.Length - 1;

            // FROZEN
            if (segCount >= 2)
            {
                for (int i = 0; i < segCount - 1; i++)
                {
                    float3 a = path[i].Position;
                    float3 b = path[i + 1].Position;
                    float3 dir = b - a; dir.y = 0;
                    float len = math.length(new float2(dir.x, dir.z));
                    if (len < 1e-4f) continue;

                    ConveyorQuantization.QuantizeStraight(len, minLen, maxLen, out int cnt, out float per);
                    var rot = quaternion.LookRotationSafe(math.normalizesafe(new float3(b.x - a.x, 0, b.z - a.z)), new float3(0, 1, 0));

                    for (int k = 0; k < cnt; k++)
                    {
                        float d = per * (k + 0.5f);
                        float t = math.saturate(d / math.max(len, 1e-4f));
                        float3 p = math.lerp(a, b, t);
                        frozenOut.Add(new ConveyorFrozenPose { Position = p, Rotation = rot, Length = per });
                    }
                }
            }

            // LIVE
            if (segCount >= 1)
            {
                float3 a = path[segCount - 1].Position;
                float3 b = path[segCount].Position;
                float3 dir = b - a; dir.y = 0;
                float len = math.length(new float2(dir.x, dir.z));

                if (len >= 1f)
                {
                    if (!em.HasComponent<ConveyorPreviewRuntime>(holder))
                        em.AddComponentData(holder, new ConveyorPreviewRuntime());

                    var runtime = em.GetComponentData<ConveyorPreviewRuntime>(holder);
                    ConveyorQuantization.QuantizeStraight(len, minLen, maxLen, out int rawCnt, out float per);

                    // гистерезис
                    int count = rawCnt;
                    float boundary = runtime.LastTailCount * maxLen;
                    float H = math.max(0.25f * maxLen, 0.5f);
                    if (runtime.LastTailCount > 0 && len > boundary - H && len < boundary + H)
                        count = runtime.LastTailCount;
                    per = math.clamp(len / math.max(count, 1), minLen, maxLen);

                    var rot = quaternion.LookRotationSafe(math.normalizesafe(new float3(b.x - a.x, 0, b.z - a.z)), new float3(0, 1, 0));
                    for (int k = 0; k < count; k++)
                    {
                        float d = per * (k + 0.5f);
                        float t = math.saturate(d / math.max(len, 1e-4f));
                        float3 p = math.lerp(a, b, t);
                        liveOut.Add(new ConveyorLivePose { Position = p, Rotation = rot, Length = per });
                    }

                    runtime.LastTailCount = count;
                    runtime.LastTailPerLen = per;
                    em.SetComponentData(holder, runtime);
                }
            }

            path.Dispose();
        }
    }
}
