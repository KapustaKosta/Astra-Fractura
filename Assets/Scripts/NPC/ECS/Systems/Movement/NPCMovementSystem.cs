using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

/// <summary>
/// Система, отвечающая за физическое перемещение NPC. Она выполняется перед основным шагом физики
/// и преобразует желаемую скорость (TargetVelocity), рассчитанную системой избегания, в реальную
/// физическую скорость (PhysicsVelocity). Реализует плавное ускорение/торможение,
/// проверку "на земле" и автоматическое преодоление невысоких препятствий (ступенек).
/// </summary>
[UpdateInGroup(typeof(BeforePhysicsSystemGroup))]
[UpdateAfter(typeof(PhysicsInitializeGroup))]
public partial class NPCMovementSystem : SystemBase
{
    private const uint NPC_CATEGORY_BIT      = 1u << 7;
    private const uint COLLIDES_WITH_DEFAULT = ~0u;
    private const float MaxAccelPerSec = 120f;
    private const float SmoothLambda   = 16f;
    private const float StartKick      = 1.20f;
    private const float MinSpeedHold   = 0.45f;

    /// <summary>
    /// Основной метод обновления системы. Получает необходимые зависимости (DeltaTime, PhysicsWorld)
    /// и планирует выполнение NPCMoveJob для всех соответствующих сущностей.
    /// </summary>
    protected override void OnUpdate()
    {
        var dt    = SystemAPI.Time.DeltaTime;
        var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;

        Dependency = new NPCMoveJob
        {
            DeltaTime      = dt,
            CollisionWorld = world
        }.ScheduleParallel(SystemAPI.QueryBuilder().
            WithAll<LocalTransform,
                PhysicsVelocity,
                PhysicsDamping,
                NPCMovementComponent>().
            WithNone<IsDeadTag>().Build(), Dependency);
    }

    /// <summary>
    /// Job, который выполняет всю основную логику перемещения для каждого NPC параллельно.
    /// </summary>
    [BurstCompile]
    private partial struct NPCMoveJob : IJobEntity
    {
        public float DeltaTime;
        [ReadOnly] public CollisionWorld CollisionWorld;

        /// <summary>
        /// Выполняется для каждой сущности. Метод реализует полный цикл физики движения:
        /// 1. Проверяет, "на земле" ли находится NPC.
        /// 2. Реализует механику преодоления ступенек с помощью лучей.
        /// 3. Управляет вертикальной скоростью (применяет гравитацию или удерживает на земле).
        /// 4. Плавно интерполирует текущую горизонтальную скорость к целевой,
        ///    используя экспоненциальное сглаживание, ограничение ускорения и "стартовый толчок".
        /// 5. При отсутствии цели применяет пассивное торможение.
        /// </summary>
        public void Execute(
            Entity entity,
            ref LocalTransform transform,
            ref PhysicsVelocity vel,
            ref PhysicsDamping damping,
            in NPCMovementComponent move
        )
        {
            if (!move.HasTarget && math.lengthsq(move.TargetVelocity) < 0.01f)
            {
                damping.Linear = GroundDamping;
                float k0 = 1f - math.exp(-SmoothLambda * DeltaTime);
                vel.Linear.x = math.lerp(vel.Linear.x, 0f, k0);
                vel.Linear.z = math.lerp(vel.Linear.z, 0f, k0);
                vel.Angular  = 0;
                return;
            }

            var hits = new NativeList<Unity.Physics.DistanceHit>(Allocator.Temp);
            var filter = new CollisionFilter
            {
                BelongsTo    = NPC_CATEGORY_BIT,
                CollidesWith = COLLIDES_WITH_DEFAULT,
                GroupIndex   = 0
            };

            float3 sphereCenter = transform.Position + new float3(0, GroundedOffset, 0);
            bool overlapped = CollisionWorld.OverlapSphere(sphereCenter, GroundedRadius, ref hits, filter);

            bool grounded = false;
            if (overlapped)
            {
                for (int i = 0; i < hits.Length; i++)
                {
                    if (hits[i].Entity == entity) continue;
                    if (math.dot(hits[i].SurfaceNormal, math.up()) > MaxSlopeCosine)
                    { grounded = true; break; }
                }
            }
            hits.Dispose();

            float3 desiredHorizVel = move.TargetVelocity;

            if (grounded && math.lengthsq(desiredHorizVel.xz) > 0.1f)
            {
                float3 fwd = math.normalize(desiredHorizVel);
                float3 rayStart = transform.Position + new float3(0, StepRayStartHeight, 0);
                float checkDist = math.max(StepForwardCheckMin, math.length(desiredHorizVel) * DeltaTime + Skin);
                var fwdInput = new RaycastInput { Start = rayStart, End = rayStart + fwd * checkDist, Filter = filter };

                if (CollisionWorld.CastRay(fwdInput, out Unity.Physics.RaycastHit fwdHit))
                {
                    if (math.abs(math.dot(fwdHit.SurfaceNormal, math.up())) < 0.2f)
                    {
                        float3 topStart = transform.Position + new float3(0, StepMaxHeight + StepClearanceUp, 0);
                        var topInput = new RaycastInput { Start = topStart, End = topStart + fwd * checkDist, Filter = filter };

                        if (!CollisionWorld.CastRay(topInput, out _))
                        {
                            float3 downStart = topStart + fwd * math.min(checkDist, fwdHit.Fraction * checkDist + StepExtraForward);
                            var downInput = new RaycastInput { Start = downStart, End = downStart - new float3(0, StepMaxHeight + 0.75f, 0), Filter = filter };
                            if (CollisionWorld.CastRay(downInput, out Unity.Physics.RaycastHit downHit))
                            {
                                float targetY = downHit.Position.y + GroundedOffset;
                                float dy = targetY - transform.Position.y;
                                if (dy > 0.01f && dy <= StepMaxHeight + 0.05f && math.dot(downHit.SurfaceNormal, math.up()) > MaxSlopeCosine)
                                {
                                    vel.Linear.y = math.max(vel.Linear.y, math.clamp(dy / DeltaTime, 0f, StepMaxRiseSpeed));
                                }
                            }
                        }
                    }
                }
            }

            if (grounded)
            {
                if (vel.Linear.y < 0f) vel.Linear.y = GroundedVerticalVelocity;
                damping.Linear = GroundDamping;
            }
            else
            {
                vel.Linear.y   = math.max(vel.Linear.y + Gravity * DeltaTime, TerminalVelocity);
                damping.Linear = AirDamping;
            }

            float2 vCur = new float2(vel.Linear.x, vel.Linear.z);
            float2 vTgt = new float2(desiredHorizVel.x, desiredHorizVel.z);

            float tgtLen = math.length(vTgt);
            if (tgtLen > 1e-4f)
            {
                float2 dir = vTgt / tgtLen;
                if (math.length(vCur) < StartKick * 0.5f)
                    vCur += dir * StartKick;

                float floor = math.max(tgtLen * MinSpeedHold, StartKick);
                if (math.length(vCur) < floor * 0.5f)
                    vTgt = dir * floor;

                float k = 1f - math.exp(-SmoothLambda * DeltaTime);
                float2 vPre = math.lerp(vCur, vTgt, k);
                float2 dv = vPre - vCur;
                float mdv = MaxAccelPerSec * DeltaTime;
                float dvl = math.length(dv);
                if (dvl > mdv) vPre = vCur + dv * (mdv / math.max(1e-6f, dvl));

                vCur = vPre;
            }
            else
            {
                float k = 1f - math.exp(-SmoothLambda * DeltaTime);
                vCur = math.lerp(vCur, float2.zero, k);
            }

            vel.Linear.x = vCur.x;
            vel.Linear.z = vCur.y;
            vel.Angular  = 0;
        }

        private const float GroundedRadius           = 0.4f;
        private const float GroundedOffset           = 0.05f;
        private const float MaxSlopeCosine           = 0.5f;
        private const float GroundDamping            = 2.0f;
        private const float AirDamping               = 0.1f;
        private const float Gravity                  = -25f;
        private const float TerminalVelocity         = -50f;
        private const float GroundedVerticalVelocity = -2f;
        private const float StepMaxHeight            = 0.30f;
        private const float StepRayStartHeight       = 0.10f;
        private const float StepForwardCheckMin      = 0.35f;
        private const float StepClearanceUp          = 0.05f;
        private const float StepExtraForward         = 0.05f;
        private const float StepMaxRiseSpeed         = 10f;
        private const float Skin                     = 0.02f;
    }
}