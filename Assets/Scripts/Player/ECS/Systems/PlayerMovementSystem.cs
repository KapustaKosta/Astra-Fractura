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
// - Перед препятствием: пробуем "шаг" (step offset): проверка вперёд -> проверка свободного места сверху -> поиск площадки вниз.
// - Если шаг невозможен: скользим вдоль препятствия (удаляем компонент скорости в нормаль стены).
// - В воздухе: обычная гравитация + ограниченный air control.
//

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(InputsSystem))]
public partial class PlayerMovementSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var deltaTime = SystemAPI.Time.DeltaTime;
        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;

        Dependency = new PlayerMovementJob
        {
            DeltaTime = deltaTime,
            CollisionWorld = physicsWorld
        }.ScheduleParallel(Dependency);
    }

    [BurstCompile]
    private partial struct PlayerMovementJob : IJobEntity
    {
        public float DeltaTime;
        [ReadOnly] public CollisionWorld CollisionWorld;

        public void Execute(
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
            // 1) Ground check + нормаль опоры 
            var sphereCenter = transform.Position + new float3(0f, controller.GroundedOffset, 0f);

            var hits = new NativeList<DistanceHit>(Allocator.Temp);
            var filter = new CollisionFilter
            {
                BelongsTo = 1u << 0,                       // по умолчанию
                CollidesWith = (uint)controller.GroundLayers, 
                GroupIndex = 0
            };

            bool overlapped = CollisionWorld.OverlapSphere(
                sphereCenter,
                controller.GroundedRadius,
                ref hits,
                filter
            );

            bool isGrounded = false;
            float3 groundNormal = new float3(0, 1, 0);
            float bestDot = -1f;

            if (overlapped)
            {
                for (int i = 0; i < hits.Length; i++)
                {
                    var h = hits[i];
                    if (h.Entity == entity) continue;

                    float d = math.dot(h.SurfaceNormal, new float3(0, 1, 0));
                    if (d > controller.MaxSlopeCosine && d > bestDot)
                    {
                        bestDot = d;
                        groundNormal = h.SurfaceNormal;
                        isGrounded = true;
                    }
                }
            }
            hits.Dispose();

            groundedState.IsGrounded = isGrounded;
            stateData.isGrounded = isGrounded;

            // 2) Таймауты прыжка/падения 
            if (stateData.jumpTimeoutDelta > 0f) stateData.jumpTimeoutDelta -= DeltaTime;
            if (stateData.fallTimeoutDelta > 0f) stateData.fallTimeoutDelta -= DeltaTime;

            // 3) Вертикальная скорость (прыжок / гравитация)
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

            // 4) Целевая горизонтальная скорость
            float targetSpeed = math.lengthsq(inputs.move) < 1e-6f
                ? 0f
                : (inputs.sprint ? controller.SprintSpeed : controller.MoveSpeed);

            float inputMag = inputs.analogMovement ? math.length(inputs.move) : 1f;
            targetSpeed *= inputMag;

            float accel = controller.SpeedChangeRate * (isGrounded ? 1f : controller.AirControlMultiplier);
            stateData.currentSpeed = math.lerp(stateData.currentSpeed, targetSpeed, DeltaTime * accel);
            if (math.abs(stateData.currentSpeed - targetSpeed) < controller.SpeedSnapThreshold)
                stateData.currentSpeed = targetSpeed;

            // направление в мировых координатах (до проекции на склон)
            float3 moveDir = new float3(0, 0, 0);
            if (math.lengthsq(inputs.move) > 1e-6f)
            {
                // Основано на повороте игрока (его local forward/right)
                float3 local = new float3(inputs.move.x, 0f, inputs.move.y);
                moveDir = math.normalize(math.mul(transform.Rotation, local));
            }

            // 5) Проекция движения на плоскость склона
            if (isGrounded && math.lengthsq(moveDir) > 1e-6f)
            {
                // v' = v - (v·n) n — убираем компонент в нормаль пола
                moveDir = math.normalize(Reject(moveDir, groundNormal));
            }

            float3 desiredHorizVel = moveDir * stateData.currentSpeed;

            // 6) STEP OFFSET + SLIDE вдоль препятствий (только на земле и при движении)
            if (isGrounded && math.lengthsq(desiredHorizVel) > 1e-6f)
            {
                // 6.1. Проверка препятствия впереди (RayCast от "ног")
                float3 fwd = math.normalize(desiredHorizVel);
                float3 rayStart = transform.Position + new float3(0, StepRayStartHeight, 0);
                float checkDist = math.max(StepForwardCheckMin, math.length(desiredHorizVel) * DeltaTime + Skin);

                Unity.Physics.RaycastHit fwdHit;
                var fwdInput = new RaycastInput
                {
                    Start = rayStart,
                    End = rayStart + fwd * checkDist,
                    Filter = filter
                };

                bool hasFrontHit = CollisionWorld.CastRay(fwdInput, out fwdHit);

                if (hasFrontHit)
                {
                    // Стена/уступ? (малый вклад по Y — почти вертикальная поверхность)
                    float wallY = math.abs(math.dot(fwdHit.SurfaceNormal, new float3(0, 1, 0)));

                    bool looksLikeWall = wallY < 0.2f;

                    bool stepped = false;

                    if (looksLikeWall)
                    {
                        // 6.2. Проверка свободного места сверху (ray на той же дистанции, но с поднятием)
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
                            // 6.3. Ищем площадку вниз в точке впереди
                            float3 downStart = topStart + fwd * math.min(checkDist, fwdHit.Fraction * checkDist + StepExtraForward);
                            var downInput = new RaycastInput
                            {
                                Start = downStart,
                                End = downStart + new float3(0, -(StepMaxHeight + 0.75f), 0),
                                Filter = filter
                            };

                            Unity.Physics.RaycastHit downHit;
                            bool hasDown = CollisionWorld.CastRay(downInput, out downHit);

                            if (hasDown)
                            {
                                float targetY = downHit.Position.y + controller.GroundedOffset; // небольшое прижатие
                                float dy = targetY - transform.Position.y;

                                // Успешный шаг — поднимаемся, если в пределах высоты шага и поверхность пригодна (не круче MaxSlope)
                                float slopeDot = math.dot(downHit.SurfaceNormal, new float3(0, 1, 0));
                                if (dy > 0.01f && dy <= StepMaxHeight + 0.05f && slopeDot > controller.MaxSlopeCosine)
                                {
                                    // Задаём вертикальную скорость, чтобы «успеть» подняться за кадр
                                    stateData.verticalVelocity = math.clamp(dy / math.max(DeltaTime, 1e-5f), 0f, StepMaxRiseSpeed);

                                    // Чуть продвигаем по горизонтали (остальная часть сделается скоростью)
                                    stepped = true;
                                }
                            }
                        }
                    }

                    if (!stepped)
                    {
                        // 6.4. Если шаг невозможен — скользим вдоль поверхности (удаляем компонент в нормаль препятствия)
                        float3 n = fwdHit.SurfaceNormal;
                        desiredHorizVel = Reject(desiredHorizVel, n);
                    }
                }
            }

            // 7) Применение демпфирования и итоговой скорости
            damping.Linear = isGrounded ? controller.GroundDamping : controller.AirDamping;

            velocity.Linear = new float3(
                desiredHorizVel.x,
                stateData.verticalVelocity,
                desiredHorizVel.z
            );

            velocity.Angular = new float3(0, 0, 0);
        }

        // Убирает проекцию A на N (A' = A - (A·N)N).
        private static float3 Reject(in float3 a, in float3 n)
        {
            float3 nn = math.normalizesafe(n);
            return a - nn * math.dot(a, nn);
        }

        private const float StepMaxHeight = 0.35f; // максимальная высота уступа (в юнитах мира)
        private const float StepRayStartHeight = 0.10f; // высота старта "луча ног" над землёй
        private const float StepForwardCheckMin = 0.40f; // минимальная дистанция вперёд для проверки шага
        private const float StepClearanceUp = 0.05f; // запас свободного места над головой при шаге
        private const float StepExtraForward = 0.05f; // небольшой "заглядывающий" шаг вперёд при касте вниз
        private const float StepMaxRiseSpeed = 10.0f; // ограничение скорости подъёма при шаге (чтобы не дёргало)
        private const float Skin = 0.02f; // маленький припуск, чтобы не упираться из-за точности
    }
}
