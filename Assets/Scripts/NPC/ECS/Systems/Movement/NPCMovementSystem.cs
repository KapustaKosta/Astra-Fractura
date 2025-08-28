using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Система физического движения NPC.
/// Управляет перемещением NPC по заданным маршрутам через обновление физической скорости.
/// Интегрируется с системой поиска пути и ИИ для реализации навигации.
/// </summary>
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
public partial class NPCMovementSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var dt = SystemAPI.Time.DeltaTime;
        var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;

        Dependency = new NPCMoveJob
        {
            DeltaTime = dt,
            CollisionWorld = world
        }.ScheduleParallel(Dependency);
    }

    [BurstCompile]
    private partial struct NPCMoveJob : IJobEntity
    {
        public float DeltaTime;
        [ReadOnly] public CollisionWorld CollisionWorld;

        public void Execute(
            Entity entity,
            ref LocalTransform transform,
            ref PhysicsVelocity vel,
            ref PhysicsDamping damping,
            in PhysicsCollider physicsCollider,
            ref NPCMovementComponent move,
            in NPCBaseMovementStats baseStats
        )
        {
            if (!move.HasTarget)
            {
                damping.Linear = GroundDamping;
                vel.Linear = new float3(0, vel.Linear.y, 0);
                vel.Angular = 0;
                return;
            }

            float3 toTarget = move.TargetPosition - transform.Position;
            float distSq = math.lengthsq(new float3(toTarget.x, 0, toTarget.z));

            // ВАЖНО: здесь используем ТЕКУЩИЙ move.StoppingDistance, который Follow-система
            // ставит маленьким для промежуточных углов и большим только для финала.
            float stopDist = math.max(move.StoppingDistance, 0.05f);

            if (distSq <= stopDist * stopDist)
            {
                damping.Linear = GroundDamping;
                if (math.lengthsq(vel.Linear.xz) <= move.VelocityZeroingThresholdSq)
                    vel.Linear = new float3(0, vel.Linear.y, 0);
                vel.Angular = 0;

                // Не снимаем HasTarget — это делает Follow на последнем угле.
#if UNITY_EDITOR
                Debug.Log($"[Move] near target: dist={math.sqrt(distSq):F2} <= stop={stopDist:F2} ; velXZ={math.length(new float2(vel.Linear.x, vel.Linear.z)):F3}");
#endif
                return;
            }

            // Дальше стандартная логика движения/ступенек 
            var hits = new NativeList<DistanceHit>(Allocator.Temp);
            var filter = new CollisionFilter
            {
                BelongsTo = 1u << 0,
                CollidesWith = GroundMask,
                GroupIndex = 0
            };

            float3 sphereCenter = transform.Position + new float3(0, GroundedOffset, 0);
            bool overlapped = CollisionWorld.OverlapSphere(sphereCenter, GroundedRadius, ref hits, filter);

            bool grounded = false;
            float3 groundN = new float3(0, 1, 0);
            float bestDot = -1f;

            if (overlapped)
            {
                for (int i = 0; i < hits.Length; i++)
                {
                    var h = hits[i];
                    if (h.Entity == entity) continue;

                    float d = math.dot(h.SurfaceNormal, new float3(0, 1, 0));
                    if (d > MaxSlopeCosine && d > bestDot)
                    {
                        bestDot = d;
                        groundN = h.SurfaceNormal;
                        grounded = true;
                    }
                }
            }
            hits.Dispose();

            float3 dirXZ = math.normalizesafe(new float3(toTarget.x, 0, toTarget.z));
            float3 moveDir = dirXZ;

            if (grounded && math.lengthsq(moveDir) > 1e-6f)
                moveDir = math.normalize(Reject(moveDir, groundN));

            float3 desiredHorizVel = moveDir * move.Speed;

            if (grounded && math.lengthsq(desiredHorizVel) > 1e-6f)
            {
                float3 fwd = math.normalize(desiredHorizVel);
                float3 rayStart = transform.Position + new float3(0, StepRayStartHeight, 0);
                float checkDist = math.max(StepForwardCheckMin, math.length(desiredHorizVel) * DeltaTime + Skin);

                var fwdInput = new RaycastInput
                {
                    Start = rayStart,
                    End = rayStart + fwd * checkDist,
                    Filter = filter
                };

                Unity.Physics.RaycastHit fwdHit;
                bool hasFront = CollisionWorld.CastRay(fwdInput, out fwdHit);

                bool stepped = false;
                if (hasFront)
                {
                    float wallY = math.abs(math.dot(fwdHit.SurfaceNormal, new float3(0, 1, 0)));
                    bool looksLikeWall = wallY < 0.2f;

                    if (looksLikeWall)
                    {
                        float3 topStart = transform.Position + new float3(0, StepMaxHeight + StepClearanceUp, 0);
                        var topInput = new RaycastInput
                        {
                            Start = topStart,
                            End = topStart + fwd * checkDist,
                            Filter = filter
                        };
                        bool blockedAbove = CollisionWorld.CastRay(topInput, out _);

                        if (!blockedAbove)
                        {
                            float3 downStart = topStart + fwd * math.min(checkDist, fwdHit.Fraction * checkDist + StepExtraForward);
                            var downInput = new RaycastInput
                            {
                                Start = downStart,
                                End = downStart + new float3(0, -(StepMaxHeight + 0.75f), 0),
                                Filter = filter
                            };

                            Unity.Physics.RaycastHit downHit;
                            if (CollisionWorld.CastRay(downInput, out downHit))
                            {
                                float targetY = downHit.Position.y + GroundedOffset;
                                float dy = targetY - transform.Position.y;
                                float slopeDot = math.dot(downHit.SurfaceNormal, new float3(0, 1, 0));

                                if (dy > 0.01f && dy <= StepMaxHeight + 0.05f && slopeDot > MaxSlopeCosine)
                                {
                                    float riseV = math.clamp(dy / math.max(DeltaTime, 1e-5f), 0f, StepMaxRiseSpeed);
                                    vel.Linear.y = math.max(vel.Linear.y, riseV);
                                    stepped = true;
#if UNITY_EDITOR
                                    Debug.Log($"[Move] stepped up dy={dy:F2}");
#endif
                                }
                            }
                        }
                    }

                    if (!stepped)
                    {
                        desiredHorizVel = Reject(desiredHorizVel, fwdHit.SurfaceNormal);
                    }
                }
            }

            if (math.lengthsq(desiredHorizVel.xz) > 1e-6f)
            {
                float3 forward = math.normalize(new float3(desiredHorizVel.x, 0, desiredHorizVel.z));
                quaternion targetRot = quaternion.LookRotationSafe(forward, new float3(0, 1, 0));
                transform.Rotation = math.slerp(transform.Rotation, targetRot, math.saturate(move.RotationSpeed * DeltaTime));
            }

            if (grounded)
            {
                if (vel.Linear.y < 0f) vel.Linear.y = GroundedVerticalVelocity;
            }
            else
            {
                vel.Linear.y = math.max(vel.Linear.y + Gravity * DeltaTime, TerminalVelocity);
            }

            damping.Linear = grounded ? GroundDamping : AirDamping;

            vel.Linear = new float3(
                desiredHorizVel.x,
                vel.Linear.y,
                desiredHorizVel.z
            );
            vel.Angular = 0;

#if UNITY_EDITOR
            Debug.DrawLine(transform.Position, move.TargetPosition, Color.white, 0.05f);
#endif
        }

        private static float3 Reject(in float3 v, in float3 n)
        {
            float3 nn = math.normalizesafe(n);
            return v - nn * math.dot(v, nn);
        }

        private const uint GroundMask = 0xFFFFFFFFu;
        private const float GroundedRadius = 0.4f;
        private const float GroundedOffset = 0.05f;
        private const float MaxSlopeCosine = 0.5f;
        private const float GroundDamping = 2.0f;
        private const float AirDamping = 0.1f;
        private const float Gravity = -25f;
        private const float TerminalVelocity = -50f;
        private const float GroundedVerticalVelocity = -2f;

        private const float StepMaxHeight = 0.30f;
        private const float StepRayStartHeight = 0.10f;
        private const float StepForwardCheckMin = 0.35f;
        private const float StepClearanceUp = 0.05f;
        private const float StepExtraForward = 0.05f;
        private const float StepMaxRiseSpeed = 10f;
        private const float Skin = 0.02f;
    }
}
