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
        const float kBeltClearance = 0.07f;
        const float kBeltFullHeight = 1.6216f;
        const float kItemWorldHeight = 0.6f;
        const float kItemHalfHeight = kItemWorldHeight / 2f;
        const float kBeltTopOffset = 0.5f * kBeltFullHeight + kBeltClearance + kItemHalfHeight;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            var transitLookup = SystemAPI.GetComponentLookup<ItemInTransit>(true);
            var jointsLookup = SystemAPI.GetBufferLookup<RouteJoint>(true);
            var powerScalingLookup = SystemAPI.GetComponentLookup<RoutePowerScaling>(true);

            var initJob = new InitJob
            {
                ECB = ecb,
                TransitLookup = transitLookup,
                JointsLookup = jointsLookup
            };
            state.Dependency = initJob.ScheduleParallel(state.Dependency);

            var moveJob = new MoveJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                TransitLookup = transitLookup,
                JointsLookup = jointsLookup,
                PowerScalingLookup = powerScalingLookup 
            };
            state.Dependency = moveJob.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ItemVisualTag), typeof(ConveyorVisualNeedsInitTag))]
        public partial struct InitJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ECB;
            [ReadOnly] public ComponentLookup<ItemInTransit> TransitLookup;
            [ReadOnly] public BufferLookup<RouteJoint> JointsLookup;

            void Execute([ChunkIndexInQuery] int chunkIndex, Entity visualEntity, ref LocalTransform transform, ref ConveyorVisualProgress progress, in VisualFor link)
            {
                if (!TransitLookup.HasComponent(link.LogicalEntity))
                {
                    ECB.RemoveComponent<ConveyorVisualNeedsInitTag>(chunkIndex, visualEntity); return;
                }

                var transit = TransitLookup[link.LogicalEntity];
                if (!JointsLookup.HasBuffer(transit.RouteEntity))
                {
                    ECB.RemoveComponent<ConveyorVisualNeedsInitTag>(chunkIndex, visualEntity); return;
                }

                var joints = JointsLookup[transit.RouteEntity];
                if (joints.Length < 2)
                {
                    ECB.RemoveComponent<ConveyorVisualNeedsInitTag>(chunkIndex, visualEntity); return;
                }

                float totalLength = 0;
                for (int i = 0; i < joints.Length - 1; i++)
                {
                    float seg = math.distance(joints[i].Position, joints[i + 1].Position);
                    if (seg > 1e-5f) totalLength += seg;
                }

                progress.TotalLength = totalLength;
                progress.Speed = totalLength > 0 ? totalLength / math.max(0.001f, transit.TravelDuration) : 0;
                progress.TotalDistanceTraveled = 0f;

                transform.Position = joints[0].Position + new float3(0, kBeltTopOffset, 0);

                for (int i = 0; i < joints.Length - 1; i++)
                {
                    float3 a = joints[i].Position;
                    float3 b = joints[i + 1].Position;
                    float3 d = b - a;
                    if (math.lengthsq(d) > 1e-8f)
                    {
                        transform.Rotation = quaternion.LookRotationSafe(math.normalize(d), math.up());
                        break;
                    }
                }

                ECB.RemoveComponent<ConveyorVisualNeedsInitTag>(chunkIndex, visualEntity);
            }
        }

        [BurstCompile]
        [WithAll(typeof(ItemVisualTag))]
        [WithNone(typeof(ConveyorVisualNeedsInitTag))]
        public partial struct MoveJob : IJobEntity
        {
            public float DeltaTime;
            [ReadOnly] public ComponentLookup<ItemInTransit> TransitLookup;
            [ReadOnly] public BufferLookup<RouteJoint> JointsLookup;
            [ReadOnly] public ComponentLookup<RoutePowerScaling> PowerScalingLookup;

            void Execute(ref ConveyorVisualProgress progress, ref LocalTransform transform, in VisualFor link)
            {
                if (!TransitLookup.HasComponent(link.LogicalEntity)) return;
                var transit = TransitLookup[link.LogicalEntity];

                if (!JointsLookup.HasBuffer(transit.RouteEntity)) return;
                var joints = JointsLookup[transit.RouteEntity];
                if (joints.Length < 2 || progress.Speed <= 0) return;

                float speedMultiplier = PowerScalingLookup.TryGetComponent(transit.RouteEntity, out var scaling)
                    ? scaling.SpeedMultiplier
                    : 1.0f;
                
                progress.TotalDistanceTraveled += progress.Speed * speedMultiplier * DeltaTime;
                progress.TotalDistanceTraveled = math.min(progress.TotalDistanceTraveled, progress.TotalLength);

                float distanceRemaining = progress.TotalDistanceTraveled;

                for (int i = 0; i < joints.Length - 1; i++)
                {
                    float3 startJoint = joints[i].Position;
                    float3 endJoint   = joints[i + 1].Position;
                    float segmentLength = math.distance(startJoint, endJoint);

                    if (segmentLength < 1e-5f) continue;

                    if (distanceRemaining <= segmentLength || i == joints.Length - 2)
                    {
                        float t = math.saturate(distanceRemaining / segmentLength);
                        float3 pos = math.lerp(startJoint, endJoint, t);
                        transform.Position = pos + new float3(0, kBeltTopOffset, 0);

                        float3 dir = endJoint - startJoint;
                        if (math.lengthsq(dir) > 1e-8f)
                            transform.Rotation = quaternion.LookRotationSafe(math.normalize(dir), math.up());

                        progress.CurrentJointIndex = i;
                        progress.DistanceOnSegment = distanceRemaining;
                        return;
                    }

                    distanceRemaining -= segmentLength;
                }
            }
        }
    }
}