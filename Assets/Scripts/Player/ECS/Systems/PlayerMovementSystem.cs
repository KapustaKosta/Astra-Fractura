using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

//
// ОСНОВНЫЕ ИДЕИ:
// - На земле: проецируем движение на плоскость склона, чтобы скорость по поверхности была постоянной.
// - Перед препятствием: "шаг" (step offset) -> если нельзя, скользим вдоль препятствия.
// - В воздухе: обычная гравитация + ограниченный air control.
// - КНОКБЕК встроен в один джоб: если есть компонент PlayerKnockback — применяем импульс и выходим.
//   Когда импульс затух — снимаем компонент через параллельный ECB.
//
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(InputsSystem))]
[UpdateBefore(typeof(PhysicsSystemGroup))] 
public partial class PlayerMovementSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var deltaTime    = SystemAPI.Time.DeltaTime;
        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;


        var knockbackLookup = SystemAPI.GetComponentLookup<PlayerKnockback>(isReadOnly: false);


        var ecbSingleton = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
        var ecbParallel  = ecbSingleton.CreateCommandBuffer(World.Unmanaged).AsParallelWriter();

        Dependency = new PlayerMovementJob
        {
            DeltaTime       = deltaTime,
            CollisionWorld  = physicsWorld,
            KnockbackLookup = knockbackLookup,
            Ecb             = ecbParallel
        }.ScheduleParallel(Dependency);

        
    }

    [BurstCompile]
    private partial struct PlayerMovementJob : IJobEntity
    {
        public float DeltaTime;
        [ReadOnly] public CollisionWorld CollisionWorld;

        // Дадим джобу доступ к кнокбеку у ТЕКУЩЕЙ сущности
        [NativeDisableParallelForRestriction]
        public ComponentLookup<PlayerKnockback> KnockbackLookup;

        public EntityCommandBuffer.ParallelWriter Ecb;

        public void Execute(
            [EntityIndexInQuery] int sortKey,
            Entity entity,
            ref LocalTransform transform,
            ref PhysicsVelocity velocity,
            ref PhysicsDamping damping,
            ref PlayerGroundedState groundedState,
            ref PlayerStateData stateData,
            in PlayerControllerData controller,
            in PlayerGroundCheckData groundCheck,
            in InputsData inputs,
            in PhysicsCollider physicsCollider
        )
        {

            bool hadKnockback = KnockbackLookup.HasComponent(entity); // RW-lookup, прокинут в джоб
            // DEBUG: if (hadKnockback) { /* log: "Knockback active, initial kVel=" + kbRef.ValueRO.Velocity */ }
            if (hadKnockback)
            {
                var kbRef = KnockbackLookup.GetRefRW(entity);
                float3 kVel = kbRef.ValueRO.Velocity;

                // Гравитация + терминальная скорость
                kVel.y += controller.Gravity * DeltaTime;
                if (kVel.y < controller.TerminalVelocity)
                    kVel.y = controller.TerminalVelocity;

                // Применяем к физике
                velocity.Linear  = kVel;
                velocity.Angular = float3.zero;

                // Затухание импульса и синхронизация
                kVel *= kbRef.ValueRO.Damping;
                stateData.verticalVelocity = kVel.y;

                // Записываем обратно в компонент
                kbRef.ValueRW.Velocity = kVel;

                // Порог завершения импульса
                const float endEpsilonSq     = 0.01f; // ~0.1 m/s
                const float groundedEpsSq    = 0.04f; // ~0.2 m/s — если на земле, снимаем щедрее

                // Быстрый чек "на земле?" (локально, только для завершения кнокбека) — с фильтрацией нормали (из предыдущего фикса)
                bool groundedQuick = false;
                float bestDotQuick = -1f;
                {
                    float3 sphereCenter = transform.Position + new float3(0f, controller.GroundedOffset, 0f);
                    var tmpHits = new NativeList<DistanceHit>(Allocator.Temp);
                    var groundFilter = new CollisionFilter
                    {
                        BelongsTo    = 1u << 0,
                        CollidesWith = (uint)controller.GroundLayers,
                        GroupIndex   = 0
                    };
                    bool overlappedQuick = CollisionWorld.OverlapSphere(
                        sphereCenter,
                        controller.GroundedRadius,
                        ref tmpHits,
                        groundFilter
                    );
                    if (overlappedQuick)
                    {
                        for (int i = 0; i < tmpHits.Length; i++)
                        {
                            var h = tmpHits[i];
                            if (h.Entity == entity) continue;

                            float d = math.dot(h.SurfaceNormal, new float3(0f, 1f, 0f));
                            if (d > controller.MaxSlopeCosine && d > bestDotQuick)
                            {
                                bestDotQuick = d;
                                groundedQuick = true;
                            }
                        }
                    }
                    tmpHits.Dispose();
                }

                // DEBUG: /* log: "Knockback: kVel=" + kVel + ", groundedQuick=" + groundedQuick + ", bestDotQuick=" + bestDotQuick + ", MaxSlopeCosine=" + controller.MaxSlopeCosine */ 

                // Решение о снятии
                float kLenSq = math.lengthsq(kVel);
                bool shouldEnd = (kLenSq <= endEpsilonSq) || (groundedQuick && kLenSq <= groundedEpsSq);

                // DEBUG: /* log: "Knockback decision: kLenSq=" + kLenSq + ", endEpsilonSq=" + endEpsilonSq + ", groundedEpsSq=" + groundedEpsSq + ", shouldEnd=" + shouldEnd */ 

                if (shouldEnd)
                {
                    // Снимаем компонент прямо сейчас (через параллельный ECB)
                    Ecb.RemoveComponent<PlayerKnockback>(sortKey, entity);
                    hadKnockback = false; 
                    velocity.Linear = new float3(0f, stateData.verticalVelocity, 0f);

                    // DEBUG: /* log: "Knockback ended, reset horiz vel to 0, vVel=" + stateData.verticalVelocity */ 
                }
                else
                {
                    // Импульс ещё идёт — обычное движение пропускаем
                    // DEBUG: /* log: "Knockback continues, skipping movement" */ 
                    return;
                }
            }
            else
            {
                // DEBUG: /* log: "No knockback, proceeding to normal movement" */ 
            }
            
            var sphereCenterMain = transform.Position + new float3(0f, controller.GroundedOffset, 0f);
            var hits = new NativeList<DistanceHit>(Allocator.Temp);
            var filter = new CollisionFilter
            {
                BelongsTo    = 1u << 0,
                CollidesWith = (uint)controller.GroundLayers,
                GroupIndex   = 0
            };

            bool overlapped = CollisionWorld.OverlapSphere(
                sphereCenterMain,
                controller.GroundedRadius,
                ref hits,
                filter
            );

            bool   isGrounded   = false;
            float3 groundNormal = new float3(0f, 1f, 0f);
            float  bestDot      = -1f;

            if (overlapped)
            {
                for (int i = 0; i < hits.Length; i++)
                {
                    var h = hits[i];
                    if (h.Entity == entity) continue;

                    float d = math.dot(h.SurfaceNormal, new float3(0f, 1f, 0f));
                    if (d > controller.MaxSlopeCosine && d > bestDot)
                    {
                        bestDot      = d;
                        groundNormal = h.SurfaceNormal;
                        isGrounded   = true;
                    }
                }
            }
            hits.Dispose();

            groundedState.IsGrounded = isGrounded;
            stateData.isGrounded     = isGrounded;

            // DEBUG: /* log: "Ground check: overlapped=" + overlapped + ", isGrounded=" + isGrounded + ", bestDot=" + bestDot + ", MaxSlopeCosine=" + controller.MaxSlopeCosine + ", hits count=" + hits.Length (но hits disposed, используйте counter) */ 


            if (stateData.jumpTimeoutDelta > 0f) stateData.jumpTimeoutDelta -= DeltaTime;
            if (stateData.fallTimeoutDelta > 0f) stateData.fallTimeoutDelta -= DeltaTime;


            if (isGrounded)
            {
                stateData.fallTimeoutDelta = controller.FallTimeout;

                // лёгкое "прижатие" к земле
                if (stateData.verticalVelocity < 0f)
                    stateData.verticalVelocity = controller.GroundedVerticalVelocity;

                // прыжок
                if (inputs.jump && stateData.jumpTimeoutDelta <= 0f)
                {
                    stateData.verticalVelocity = math.sqrt(controller.JumpHeight * -2f * controller.Gravity);
                    stateData.jumpTimeoutDelta = controller.JumpTimeout;
                }
            }
            else
            {
                // Coyote-time (опционально): позволяем прыгнуть короткое время после схода с земли
                if (inputs.jump && stateData.fallTimeoutDelta > 0f)
                {
                    stateData.verticalVelocity = math.sqrt(controller.JumpHeight * -2f * controller.Gravity);
                    stateData.fallTimeoutDelta = 0f;
                }

                stateData.verticalVelocity += controller.Gravity * DeltaTime;
                if (stateData.verticalVelocity < controller.TerminalVelocity)
                    stateData.verticalVelocity = controller.TerminalVelocity;
            }

            // DEBUG: /* log: "Vertical: isGrounded=" + isGrounded + ", vVel after=" + stateData.verticalVelocity + ", jump pressed=" + inputs.jump + ", fallTimeout=" + stateData.fallTimeoutDelta */ 
            
            float2 inputVec   = inputs.move;
            float  targetSpeed = math.lengthsq(inputVec) < 1e-6f
                ? 0f
                : (inputs.sprint ? controller.SprintSpeed : controller.MoveSpeed);

            float inputMag = inputs.analogMovement ? math.length(inputVec) : 1f;
            targetSpeed *= inputMag;

            float accel = controller.SpeedChangeRate * (isGrounded ? 1f : controller.AirControlMultiplier);
            stateData.currentSpeed = math.lerp(stateData.currentSpeed, targetSpeed, DeltaTime * accel);
            if (math.abs(stateData.currentSpeed - targetSpeed) < controller.SpeedSnapThreshold)
                stateData.currentSpeed = targetSpeed;

            float3 moveDir = new float3(0f, 0f, 0f);
            if (math.lengthsq(inputVec) > 1e-6f)
            {
                float3 local = new float3(inputVec.x, 0f, inputVec.y);
                moveDir = math.normalize(math.mul(transform.Rotation, local));
            }

            // 5) Проекция движения на плоскость склона
            if (isGrounded && math.lengthsq(moveDir) > 1e-6f)
                moveDir = math.normalize(moveDir - math.normalizesafe(groundNormal) * math.dot(moveDir, math.normalizesafe(groundNormal)));

            float3 desiredHorizVel = moveDir * stateData.currentSpeed;

            // DEBUG: /* log: "Horizontal: inputVec=" + inputVec + ", targetSpeed=" + targetSpeed + ", currentSpeed before lerp=" + stateData.currentSpeed + ", accel=" + accel + ", airMultiplier=" + controller.AirControlMultiplier + ", moveDir len=" + math.length(moveDir) + ", desiredHorizVel len=" + math.length(desiredHorizVel) */ 
            
            if (isGrounded && math.lengthsq(desiredHorizVel) > 1e-6f)
            {
                const float StepMaxHeight        = 0.35f;
                const float StepRayStartHeight   = 0.10f;
                const float StepForwardCheckMin  = 0.40f;
                const float StepClearanceUp      = 0.05f;
                const float StepExtraForward     = 0.05f;
                const float StepMaxRiseSpeed     = 10.0f;
                const float Skin                 = 0.02f;

                float3 fwd       = math.normalize(desiredHorizVel);
                float3 rayStart  = transform.Position + new float3(0f, StepRayStartHeight, 0f);
                float  checkDist = math.max(StepForwardCheckMin, math.length(desiredHorizVel) * DeltaTime + Skin);

                var fwdInput = new RaycastInput
                {
                    Start  = rayStart,
                    End    = rayStart + fwd * checkDist,
                    Filter = filter
                };

                bool hasFrontHit = CollisionWorld.CastRay(fwdInput, out var fwdHit);

                if (hasFrontHit)
                {
                    float wallY         = math.abs(math.dot(fwdHit.SurfaceNormal, new float3(0f, 1f, 0f)));
                    bool  looksLikeWall = wallY < 0.2f;
                    bool  stepped       = false;

                    // DEBUG: /* log: "Step check: hasFrontHit=" + hasFrontHit + ", looksLikeWall=" + looksLikeWall + ", wallY=" + wallY */ 

                    if (looksLikeWall)
                    {
                        float3 topStart = transform.Position + new float3(0f, StepMaxHeight + StepClearanceUp, 0f);
                        var topInput = new RaycastInput
                        {
                            Start  = topStart,
                            End    = topStart + fwd * checkDist,
                            Filter = filter
                        };

                        if (!CollisionWorld.CastRay(topInput, out _))
                        {
                            float3 downStart = topStart + fwd * math.min(checkDist, fwdHit.Fraction * checkDist + StepExtraForward);
                            var downInput = new RaycastInput
                            {
                                Start  = downStart,
                                End    = downStart + new float3(0f, -(StepMaxHeight + 0.75f), 0f),
                                Filter = filter
                            };

                            if (CollisionWorld.CastRay(downInput, out var downHit))
                            {
                                float targetY  = downHit.Position.y + controller.GroundedOffset;
                                float dy       = targetY - transform.Position.y;
                                float slopeDot = math.dot(downHit.SurfaceNormal, new float3(0f, 1f, 0f));
                                if (dy > 0.01f && dy <= StepMaxHeight + 0.05f && slopeDot > controller.MaxSlopeCosine)
                                {
                                    stateData.verticalVelocity = math.clamp(dy / math.max(DeltaTime, 1e-5f), 0f, StepMaxRiseSpeed);
                                    stepped = true;
                                }
                            }
                        }
                    }

                    if (!stepped)
                        desiredHorizVel = desiredHorizVel - math.normalizesafe(fwdHit.SurfaceNormal) * math.dot(desiredHorizVel, math.normalizesafe(fwdHit.SurfaceNormal));

                    // DEBUG: /* log: "Step result: stepped=" + stepped + ", final desiredHorizVel len=" + math.length(desiredHorizVel) */ 
                }
            }


            damping.Linear = isGrounded ? controller.GroundDamping : controller.AirDamping;

            velocity.Linear = new float3(
                desiredHorizVel.x,
                stateData.verticalVelocity,
                desiredHorizVel.z
            );
            velocity.Angular = float3.zero;

            // DEBUG: /* log: "Final: damping=" + damping.Linear + ", final vel len horiz=" + math.length(new float3(velocity.Linear.x, 0, velocity.Linear.z)) + ", v y=" + velocity.Linear.y + ", isGrounded=" + isGrounded */ 
        }

        // Убирает проекцию A на N (A' = A - (A·N)N).
        private static float3 Reject(in float3 a, in float3 n)
        {
            float3 nn = math.normalizesafe(n);
            return a - nn * math.dot(a, nn);
        }

        private const float StepMaxHeight       = 0.35f;
        private const float StepRayStartHeight  = 0.10f;
        private const float StepForwardCheckMin = 0.40f;
        private const float StepClearanceUp     = 0.05f;
        private const float StepExtraForward    = 0.05f;
        private const float StepMaxRiseSpeed    = 10.0f;
        private const float Skin                = 0.02f;
    }
}