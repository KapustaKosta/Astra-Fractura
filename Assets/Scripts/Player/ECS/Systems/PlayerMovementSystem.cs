using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine; 

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PhysicsSystemGroup))]
public partial class PlayerMovementSystem : SystemBase
{
    protected override void OnUpdate()
    {
        if (!SystemAPI.QueryBuilder().WithAll<PlayerTag, DeadTag>().Build().IsEmpty)
        {
            foreach(var velocity in SystemAPI.Query<RefRW<PhysicsVelocity>>().WithAll<PlayerTag, DeadTag>())
            {
                velocity.ValueRW.Linear = float3.zero;
                velocity.ValueRW.Angular = float3.zero;
            }
            return;
        }

        float dt = SystemAPI.Time.DeltaTime;
        var collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;

        var endFixedECB = World.GetOrCreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();
        var ecbParallel = endFixedECB.CreateCommandBuffer().AsParallelWriter();

        var knockbackLookup = SystemAPI.GetComponentLookup<PlayerKnockback>(isReadOnly: false);

        const float AirAccel = 10f;
        const float AirDrag  = 1.2f;

        Dependency = new MoveJob
        {
            DeltaTime       = dt,
            CollisionWorld  = collisionWorld,
            Ecb             = ecbParallel,
            KnockbackLookup = knockbackLookup,
            AirAccel        = AirAccel,
            AirDrag         = AirDrag
        }
        .ScheduleParallel(Dependency);

        endFixedECB.AddJobHandleForProducer(Dependency);
    }

    [BurstCompile]
    private partial struct MoveJob : IJobEntity
    {
        public float DeltaTime;
        [ReadOnly] public CollisionWorld CollisionWorld;

        // Дадим джобу доступ к кнокбеку у ТЕКУЩЕЙ сущности
        [NativeDisableParallelForRestriction]
        public ComponentLookup<PlayerKnockback> KnockbackLookup;

        public EntityCommandBuffer.ParallelWriter Ecb;

        public float AirAccel;
        public float AirDrag;

        public void Execute(
            [EntityIndexInQuery] int sortKey,
            Entity entity,
            ref LocalTransform transform,
            ref PhysicsVelocity velocity,
            ref PhysicsDamping damping,
            ref PlayerGroundedState groundedState,
            ref PlayerStateData stateData,
            in  PlayerControllerData controller,
            ref PlayerGroundCheckData groundCheck,
            in  InputsData inputs,
            in  PhysicsCollider collider
        )
        {
            // Ground check
            bool isGrounded = false;
            float3 groundNormal = new float3(0,1,0);

            {
                var filter = new CollisionFilter
                {
                    BelongsTo    = 1u << 0,
                    CollidesWith = (uint)controller.GroundLayers,
                    GroupIndex   = 0
                };

                float3 start  = transform.Position + new float3(0f, controller.GroundedOffset + 0.05f, 0f);
                float  castLn = controller.GroundedRadius + 0.20f;

                var ray = new RaycastInput
                {
                    Start  = start,
                    End    = start + new float3(0f, -castLn, 0f),
                    Filter = filter
                };

                if (CollisionWorld.CastRay(ray, out var hit))
                {
                    if (math.dot(hit.SurfaceNormal, new float3(0,1,0)) > controller.MaxSlopeCosine)
                    {
                        isGrounded   = true;
                        groundNormal = hit.SurfaceNormal;
                    }
                }
            }

            groundedState.IsGrounded = isGrounded;
            stateData.isGrounded     = isGrounded;

            if (stateData.jumpTimeoutDelta > 0f) stateData.jumpTimeoutDelta -= DeltaTime;
            if (stateData.fallTimeoutDelta > 0f) stateData.fallTimeoutDelta -= DeltaTime;

            // Кнокбек
            if (KnockbackLookup.HasComponent(entity))
            {
                var kb = KnockbackLookup.GetRefRW(entity);
                float3 kVel = kb.ValueRO.Velocity;

                float2 h = new float2(kVel.x, kVel.z) * kb.ValueRO.Damping;
                float vy = kVel.y;
                if (isGrounded && vy < 0f) vy = 0f;
                vy *= kb.ValueRO.Damping;

                vy += controller.Gravity * DeltaTime;
                if (vy < controller.TerminalVelocity) vy = controller.TerminalVelocity;

                if (isGrounded) h *= 0.85f;

                kVel = new float3(h.x, vy, h.y);

                velocity.Linear  = kVel;
                velocity.Angular = float3.zero;
                stateData.verticalVelocity = vy;
                kb.ValueRW.Velocity = kVel;

                const float epsAirSq    = 0.01f;
                const float epsGroundSq = 0.04f;
                float hSq = math.lengthsq(h);
                float tot = math.lengthsq(kVel);

                bool endNow = isGrounded ? (hSq <= epsGroundSq) : (tot <= epsAirSq);
                if (endNow)
                {
                    stateData.verticalVelocity = isGrounded ? controller.GroundedVerticalVelocity : stateData.verticalVelocity;
                    Ecb.RemoveComponent<PlayerKnockback>(sortKey, entity);
                }
                else
                {
                    return;
                }
            }

            // Обычное движение 
            float2 input = inputs.move;
            if (math.lengthsq(input) > 1f) input = math.normalize(input);
            float inputMag = math.clamp(math.length(input), 0f, 1f);

            float3 fwd = math.forward(transform.Rotation); fwd.y = 0f; fwd = math.normalizesafe(fwd);
            if (math.lengthsq(fwd) < 1e-6f) fwd = new float3(0,0,1);
            float3 right = math.normalizesafe(math.cross(new float3(0,1,0), fwd));

            float3 wishDir = math.normalizesafe(fwd * input.y + right * input.x);

            float targetSpeed = controller.MoveSpeed * inputMag;

            float3 desiredHoriz = wishDir * targetSpeed;
            if (isGrounded)
            {
                float3 along = ProjectOnPlane(desiredHoriz, groundNormal);
                desiredHoriz = math.lengthsq(along) > 1e-6f ? math.normalizesafe(along) * targetSpeed : float3.zero;
            }

            float3 curr = velocity.Linear;
            float3 currHoriz = new float3(curr.x, 0f, curr.z);

            float3 newHoriz;
            if (isGrounded)
            {
                newHoriz = desiredHoriz;

                if (inputMag < 1e-6f && math.lengthsq(newHoriz) > 1e-6f)
                {
                    newHoriz = MoveTowards(currHoriz, float3.zero, controller.MoveSpeed * DeltaTime);
                }
            }
            else
            {
                newHoriz = MoveTowards(currHoriz, wishDir * targetSpeed, AirAccel * DeltaTime);

                if (inputMag < 1e-6f) newHoriz *= math.max(0f, 1f - AirDrag * DeltaTime);
            }

            float vyNew = stateData.verticalVelocity;

            if (isGrounded)
            {
                if (inputs.jump && stateData.jumpTimeoutDelta <= 0f)
                {
                    vyNew = math.sqrt(2f * math.abs(controller.Gravity) * controller.JumpHeight);
                    stateData.jumpTimeoutDelta = controller.JumpTimeout;
                    isGrounded = false;
                    groundedState.IsGrounded = false;
                }
                else
                {
                    if (vyNew < controller.GroundedVerticalVelocity) vyNew = controller.GroundedVerticalVelocity;
                }
            }
            else
            {
                vyNew += controller.Gravity * DeltaTime;
                if (vyNew < controller.TerminalVelocity) vyNew = controller.TerminalVelocity;
            }

            velocity.Linear  = new float3(newHoriz.x, vyNew, newHoriz.z);
            velocity.Angular = float3.zero;
            stateData.verticalVelocity = vyNew;

            SlideAgainstWall(ref velocity, ref transform, in controller);
        }

        private static float3 ProjectOnPlane(in float3 v, in float3 n)
        {
            float3 nn = math.normalizesafe(n);
            return v - nn * math.dot(v, nn);
        }

        private static float3 MoveTowards(in float3 current, in float3 target, float maxDelta)
        {
            float3 delta = target - current;
            float dist = math.length(delta);
            if (dist <= maxDelta || dist < 1e-6f) return target;
            return current + delta * (maxDelta / dist);
        }

        private void SlideAgainstWall(ref PhysicsVelocity velocity, ref LocalTransform transform, in PlayerControllerData controller)
        {
            var filter = new CollisionFilter
            {
                BelongsTo    = 1u << 0,
                CollidesWith = (uint)controller.GroundLayers,
                GroupIndex   = 0
            };

            float3 v = velocity.Linear; float3 horiz = new float3(v.x, 0, v.z);
            float spd = math.length(horiz);
            if (spd < 1e-6f) return;

            float3 dir = horiz / math.max(spd, 1e-6f);
            float3 origin = transform.Position + new float3(0f, controller.GroundedOffset + 0.05f, 0f);
            float dist = math.max(0.3f, spd * DeltaTime + 0.02f);

            var ray = new RaycastInput { Start = origin, End = origin + dir * dist, Filter = filter };
            if (CollisionWorld.CastRay(ray, out var hit))
            {
                if (math.dot(hit.SurfaceNormal, new float3(0,1,0)) < 0.2f)
                {
                    float3 slideDir = math.normalizesafe(horiz - hit.SurfaceNormal * math.dot(horiz, hit.SurfaceNormal));
                    velocity.Linear = new float3(slideDir.x * spd, velocity.Linear.y, slideDir.z * spd);
                }
            }
        }
    }
}