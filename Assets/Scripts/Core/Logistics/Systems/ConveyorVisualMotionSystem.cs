using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Conveyor
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct ConveyorVisualMotionSystem : ISystem
    {
        // Геометрия/высоты
        const float kBeltFullHeight = 1.6216f;
        const float kBeltClearance = 0.02f;
        const float kItemWorldHeight = 0.6f;
        const float kItemHalfHeight = kItemWorldHeight / 2f;
        const float kBeltTopOffset = 0.5f * kBeltFullHeight + kBeltClearance + kItemHalfHeight;

        // Длины/точности
        const float kDefaultSegmentLen = 8.108f; // fallback длина секции, если нет данных
        const float kMinLen = 1e-4f;             // минимальная длина
        const float kRelEps = 1e-4f;             // относительный eps для устойчивых сравнений

        EntityQuery _initQ;
        EntityQuery _moveQ;

        public void OnCreate(ref SystemState state)
        {
            _initQ = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<ItemVisualTag>(),
                    ComponentType.ReadOnly<VisualFor>(),
                    ComponentType.ReadOnly<ConveyorVisualNeedsInitTag>(),
                    ComponentType.ReadWrite<ConveyorVisualProgress>(),
                    ComponentType.ReadWrite<LocalTransform>()
                }
            });

            _moveQ = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<ItemVisualTag>(),
                    ComponentType.ReadOnly<VisualFor>(),
                    ComponentType.ReadWrite<ConveyorVisualProgress>(),
                    ComponentType.ReadWrite<LocalTransform>()
                },
                None = new[] { ComponentType.ReadOnly<ConveyorVisualNeedsInitTag>() }
            });
        }

        public void OnUpdate(ref SystemState state)
        {
            var endSimEcb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = endSimEcb.CreateCommandBuffer(state.WorldUnmanaged);
            var ecbPar = ecb.AsParallelWriter();

            var pathLk = state.GetBufferLookup<RoutePathElement>(true);
            var ltwLk = state.GetComponentLookup<LocalToWorld>(true);
            var transitLk = state.GetComponentLookup<ItemInTransit>(true);
            var segCfgLk = state.GetComponentLookup<ConveyorSegmentSettings>(true);
            var routeLk = state.GetComponentLookup<RouteDefinition>(true);
            var connLk = state.GetComponentLookup<ConveyorConnector>(true);

            // INIT
            var initJob = new InitJob
            {
                ECB = ecbPar,
                PathLk = pathLk,
                LtwLk = ltwLk,
                TransitLk = transitLk,
                SegCfgLk = segCfgLk,
                RouteLk = routeLk,
                ConnLk = connLk
            };
            state.Dependency = initJob.ScheduleParallel(_initQ, state.Dependency);

            // MOVE
            var moveJob = new MoveJob
            {
                Now = (float)SystemAPI.Time.ElapsedTime,
                PathLk = pathLk,
                LtwLk = ltwLk,
                TransitLk = transitLk,
                SegCfgLk = segCfgLk,
                RouteLk = routeLk,
                ConnLk = connLk
            };
            state.Dependency = moveJob.ScheduleParallel(_moveQ, state.Dependency);
        }

        // INIT 
        [BurstCompile]
        [WithAll(typeof(ItemVisualTag), typeof(ConveyorVisualNeedsInitTag))]
        public partial struct InitJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ECB;

            [ReadOnly] public BufferLookup<RoutePathElement> PathLk;
            [ReadOnly] public ComponentLookup<LocalToWorld> LtwLk;
            [ReadOnly] public ComponentLookup<ItemInTransit> TransitLk;
            [ReadOnly] public ComponentLookup<ConveyorSegmentSettings> SegCfgLk;
            [ReadOnly] public ComponentLookup<RouteDefinition> RouteLk;
            [ReadOnly] public ComponentLookup<ConveyorConnector> ConnLk;

            void Execute([ChunkIndexInQuery] int sortKey,
                         Entity visual,
                         ref LocalTransform xform,
                         ref ConveyorVisualProgress prog,
                         in VisualFor link)
            {
                if (link.LogicalEntity == Entity.Null || !TransitLk.HasComponent(link.LogicalEntity))
                {
                    ECB.RemoveComponent<ConveyorVisualNeedsInitTag>(sortKey, visual);
                    return;
                }

                var tr = TransitLk[link.LogicalEntity];
                if (tr.RouteEntity == Entity.Null ||
                    !PathLk.HasBuffer(tr.RouteEntity) ||
                    !RouteLk.HasComponent(tr.RouteEntity))
                {
                    ECB.RemoveComponent<ConveyorVisualNeedsInitTag>(sortKey, visual);
                    return;
                }

                var path = PathLk[tr.RouteEntity];
                if (path.Length == 0)
                {
                    ECB.RemoveComponent<ConveyorVisualNeedsInitTag>(sortKey, visual);
                    return;
                }

                var route = RouteLk[tr.RouteEntity];
                float3 endPointRaw = GetConnectorPosition(route.EndConnector, LtwLk);

                var lens = new NativeList<float>(Allocator.Temp);
                var dirs = new NativeList<float3>(Allocator.Temp);
                var ups = new NativeList<float3>(Allocator.Temp);
                var S = new NativeList<float3>(Allocator.Temp);
                var cum = new NativeList<float>(Allocator.Temp);
                BuildChain(path, endPointRaw, ref lens, ref dirs, ref ups, ref S, ref cum);

                float totalLen = math.max(kMinLen, cum[^1] + lens[^1]);

                xform = LocalTransform.FromPosition(S[0] + ups[0] * kBeltTopOffset);

                prog.Distance = 0f;
                prog.SegmentIndex = 0;
                prog.SegmentStartDist = 0f;
                prog.TotalLength = totalLen;
                prog.Speed = totalLen / math.max(0.001f, tr.TravelDuration);

                lens.Dispose(); dirs.Dispose(); ups.Dispose(); S.Dispose(); cum.Dispose();
                ECB.RemoveComponent<ConveyorVisualNeedsInitTag>(sortKey, visual);
            }

            float3 GetConnectorPosition(Entity connector, ComponentLookup<LocalToWorld> ltw)
            {
                return ltw.HasComponent(connector) ? ltw[connector].Position : float3.zero;
            }

            float SegLen(int i, DynamicBuffer<RoutePathElement> path)
            {
                var e = path[i].SegmentEntity;
                if (SegCfgLk.HasComponent(e))
                    return math.max(kMinLen, SegCfgLk[e].Length);

                if (i + 1 < path.Length &&
                    LtwLk.HasComponent(path[i + 1].SegmentEntity) &&
                    LtwLk.HasComponent(e))
                {
                    var a = LtwLk[e].Position;
                    var b = LtwLk[path[i + 1].SegmentEntity].Position;
                    return math.max(kMinLen, math.length(b - a));
                }
                return kDefaultSegmentLen;
            }

            float3 SegUp(int i, DynamicBuffer<RoutePathElement> path)
            {
                var cur = path[i].SegmentEntity;
                return LtwLk.HasComponent(cur) ? LtwLk[cur].Up : new float3(0, 1, 0);
            }

            float3 DirToNextOrPrev(int i, DynamicBuffer<RoutePathElement> path)
            {
                var cur = path[i].SegmentEntity;
                if (!LtwLk.HasComponent(cur)) return new float3(0, 0, 1);

                var curPos = LtwLk[cur].Position;

                if (i + 1 < path.Length && LtwLk.HasComponent(path[i + 1].SegmentEntity))
                {
                    var nxtPos = LtwLk[path[i + 1].SegmentEntity].Position;
                    var v = nxtPos - curPos;
                    if (math.lengthsq(v) > 1e-10f) return math.normalize(v);
                }
                if (i > 0 && LtwLk.HasComponent(path[i - 1].SegmentEntity))
                {
                    var prvPos = LtwLk[path[i - 1].SegmentEntity].Position;
                    var v = curPos - prvPos;
                    if (math.lengthsq(v) > 1e-10f) return math.normalize(v);
                }
                return LtwLk[cur].Forward;
            }

            float3 DirToEndProjected(int i, DynamicBuffer<RoutePathElement> path, float3 endPointProjected)
            {
                var cur = path[i].SegmentEntity;
                if (!LtwLk.HasComponent(cur)) return new float3(0, 0, 1);
                var curPos = LtwLk[cur].Position;
                var v = endPointProjected - curPos;
                if (math.lengthsq(v) > 1e-10f) return math.normalize(v);
                return DirToNextOrPrev(i, path);
            }

            void BuildChain(DynamicBuffer<RoutePathElement> path, float3 endPointRaw,
                            ref NativeList<float> lens,
                            ref NativeList<float3> dirs,
                            ref NativeList<float3> ups,
                            ref NativeList<float3> S,
                            ref NativeList<float> cum)
            {
                int n = path.Length;
                lens.ResizeUninitialized(n);
                dirs.ResizeUninitialized(n);
                ups.ResizeUninitialized(n);
                S.ResizeUninitialized(n);
                cum.ResizeUninitialized(n);

                lens[0] = SegLen(0, path);
                ups[0] = SegUp(0, path);

                for (int i = 1; i < n; i++)
                    ups[i] = SegUp(i, path);

                int last = n - 1;
                var lastCenter = LtwLk[path[last].SegmentEntity].Position;
                var lastUp = ups[last];

                float3 endPointProjected = ProjectPointToPlane(endPointRaw, lastCenter, lastUp);

                dirs[0] = (n == 1) ? DirToEndProjected(0, path, endPointProjected) : DirToNextOrPrev(0, path);

                var c0 = LtwLk[path[0].SegmentEntity].Position;
                S[0] = c0 - dirs[0] * (0.5f * lens[0]);
                cum[0] = 0f;

                for (int i = 1; i < n; i++)
                {
                    lens[i] = SegLen(i, path);
                    dirs[i] = (i == n - 1) ? DirToEndProjected(i, path, endPointProjected) : DirToNextOrPrev(i, path);

                    S[i] = S[i - 1] + dirs[i - 1] * lens[i - 1];
                    cum[i] = cum[i - 1] + lens[i - 1];
                }

                float3 dirLast = dirs[last];
                float projLen = math.dot(endPointProjected - S[last], dirLast);
                float rawLen = math.length(endPointProjected - S[last]);
                float final = math.clamp(projLen, kMinLen, rawLen);

                lens[last] = final;
            }

            float3 ProjectPointToPlane(float3 point, float3 planePoint, float3 planeNormal)
            {
                float3 v = point - planePoint;
                float d = math.dot(v, planeNormal);
                return point - d * planeNormal;
            }
        }

        // MOVE
        [BurstCompile]
        [WithAll(typeof(ItemVisualTag))]
        [WithNone(typeof(ConveyorVisualNeedsInitTag))]
        public partial struct MoveJob : IJobEntity
        {
            public float Now;

            [ReadOnly] public BufferLookup<RoutePathElement> PathLk;
            [ReadOnly] public ComponentLookup<LocalToWorld> LtwLk;
            [ReadOnly] public ComponentLookup<ItemInTransit> TransitLk;
            [ReadOnly] public ComponentLookup<ConveyorSegmentSettings> SegCfgLk;
            [ReadOnly] public ComponentLookup<RouteDefinition> RouteLk;
            [ReadOnly] public ComponentLookup<ConveyorConnector> ConnLk;

            void Execute(ref LocalTransform xform,
                         ref ConveyorVisualProgress prog,
                         in VisualFor link)
            {
                if (link.LogicalEntity == Entity.Null || !TransitLk.HasComponent(link.LogicalEntity))
                    return;

                var tr = TransitLk[link.LogicalEntity];
                if (tr.RouteEntity == Entity.Null ||
                    !PathLk.HasBuffer(tr.RouteEntity) ||
                    !RouteLk.HasComponent(tr.RouteEntity))
                    return;

                var path = PathLk[tr.RouteEntity];
                if (path.Length == 0 || prog.TotalLength <= 0f)
                    return;

                var route = RouteLk[tr.RouteEntity];
                float3 endPointRaw = LtwLk.HasComponent(route.EndConnector) ? LtwLk[route.EndConnector].Position : float3.zero;

                var lens = new NativeList<float>(Allocator.Temp);
                var dirs = new NativeList<float3>(Allocator.Temp);
                var ups = new NativeList<float3>(Allocator.Temp);
                var S = new NativeList<float3>(Allocator.Temp);
                var cum = new NativeList<float>(Allocator.Temp);
                BuildChain(path, endPointRaw, ref lens, ref dirs, ref ups, ref S, ref cum);

                float totalLen = math.max(prog.TotalLength, 1e-3f);

                float tau = math.saturate((Now - tr.StartTime) / math.max(0.001f, tr.TravelDuration));
                float newDist = tau * totalLen;

                int segIdx = FindSegmentIndex(newDist, in cum, in lens);
                float segStart = cum[segIdx];
                float segLen = math.max(lens[segIdx], 1e-6f);
                float local = math.clamp(newDist - segStart, 0f, segLen);

                float3 pos = S[segIdx] + dirs[segIdx] * local + ups[segIdx] * kBeltTopOffset;
                xform = LocalTransform.FromPosition(pos);

                prog.Distance = newDist;
                prog.SegmentIndex = segIdx;
                prog.SegmentStartDist = segStart;

                lens.Dispose(); dirs.Dispose(); ups.Dispose(); S.Dispose(); cum.Dispose();
            }

            float SegLen(int i, DynamicBuffer<RoutePathElement> path)
            {
                var e = path[i].SegmentEntity;
                if (SegCfgLk.HasComponent(e))
                    return math.max(1e-4f, SegCfgLk[e].Length);

                if (i + 1 < path.Length &&
                    LtwLk.HasComponent(path[i + 1].SegmentEntity) &&
                    LtwLk.HasComponent(e))
                {
                    var a = LtwLk[e].Position;
                    var b = LtwLk[path[i + 1].SegmentEntity].Position;
                    return math.max(1e-4f, math.length(b - a));
                }
                return kDefaultSegmentLen;
            }

            float3 SegUp(int i, DynamicBuffer<RoutePathElement> path)
            {
                var cur = path[i].SegmentEntity;
                return LtwLk.HasComponent(cur) ? LtwLk[cur].Up : new float3(0, 1, 0);
            }

            float3 DirToNextOrPrev(int i, DynamicBuffer<RoutePathElement> path)
            {
                var cur = path[i].SegmentEntity;
                if (!LtwLk.HasComponent(cur)) return new float3(0, 0, 1);

                var curPos = LtwLk[cur].Position;

                if (i + 1 < path.Length && LtwLk.HasComponent(path[i + 1].SegmentEntity))
                {
                    var nxtPos = LtwLk[path[i + 1].SegmentEntity].Position;
                    var v = nxtPos - curPos;
                    if (math.lengthsq(v) > 1e-10f) return math.normalize(v);
                }
                if (i > 0 && LtwLk.HasComponent(path[i - 1].SegmentEntity))
                {
                    var prvPos = LtwLk[path[i - 1].SegmentEntity].Position;
                    var v = curPos - prvPos;
                    if (math.lengthsq(v) > 1e-10f) return math.normalize(v);
                }
                return LtwLk[cur].Forward;
            }

            float3 DirToEndProjected(int i, DynamicBuffer<RoutePathElement> path, float3 endPointProjected)
            {
                var cur = path[i].SegmentEntity;
                if (!LtwLk.HasComponent(cur)) return new float3(0, 0, 1);
                var curPos = LtwLk[cur].Position;
                var v = endPointProjected - curPos;
                if (math.lengthsq(v) > 1e-10f) return math.normalize(v);
                return DirToNextOrPrev(i, path);
            }

            void BuildChain(DynamicBuffer<RoutePathElement> path, float3 endPointRaw,
                            ref NativeList<float> lens,
                            ref NativeList<float3> dirs,
                            ref NativeList<float3> ups,
                            ref NativeList<float3> S,
                            ref NativeList<float> cum)
            {
                int n = path.Length;
                lens.ResizeUninitialized(n);
                dirs.ResizeUninitialized(n);
                ups.ResizeUninitialized(n);
                S.ResizeUninitialized(n);
                cum.ResizeUninitialized(n);

                lens[0] = SegLen(0, path);
                ups[0] = SegUp(0, path);
                for (int i = 1; i < n; i++)
                    ups[i] = SegUp(i, path);

                int last = n - 1;
                var lastCenter = LtwLk[path[last].SegmentEntity].Position;
                var lastUp = ups[last];

                float3 endPointProjected = ProjectPointToPlane(endPointRaw, lastCenter, lastUp);

                dirs[0] = (n == 1) ? DirToEndProjected(0, path, endPointProjected) : DirToNextOrPrev(0, path);

                var c0 = LtwLk[path[0].SegmentEntity].Position;
                S[0] = c0 - dirs[0] * (0.5f * lens[0]);
                cum[0] = 0f;

                for (int i = 1; i < n; i++)
                {
                    lens[i] = SegLen(i, path);
                    dirs[i] = (i == n - 1) ? DirToEndProjected(i, path, endPointProjected) : DirToNextOrPrev(i, path);

                    S[i] = S[i - 1] + dirs[i - 1] * lens[i - 1];
                    cum[i] = cum[i - 1] + lens[i - 1];
                }

                float3 dirLast = dirs[last];
                float projLen = math.dot(endPointProjected - S[last], dirLast);
                float raw = math.length(endPointProjected - S[last]);
                float final = math.clamp(projLen, kMinLen, raw);

                lens[last] = final;
            }

            int FindSegmentIndex(float dist, in NativeList<float> cum, in NativeList<float> lens)
            {
                int n = cum.Length;
                for (int i = 0; i < n - 1; i++)
                {
                    float a = cum[i];
                    float b = cum[i] + lens[i];
                    float eps = math.max(kRelEps * math.max(1f, math.abs(b)), 1e-6f);
                    if (dist <= b + eps) return i;
                }
                return n - 1;
            }

            float3 ProjectPointToPlane(float3 point, float3 planePoint, float3 planeNormal)
            {
                float3 v = point - planePoint;
                float d = math.dot(v, planeNormal);
                return point - d * planeNormal;
            }
        }
    }
}