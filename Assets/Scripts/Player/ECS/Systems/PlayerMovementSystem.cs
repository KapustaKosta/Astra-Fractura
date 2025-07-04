using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

/// <summary>
/// Система движения игрока в FixedStepSimulationSystemGroup после InputsSystem.
/// Отвечает за расчет и применение физических сил для перемещения игрока,
/// обработку прыжков и проверку заземления.
/// </summary>
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(InputsSystem))]
public partial class PlayerMovementSystem : SystemBase
{
    /// <summary>
    /// Вызывается каждый физический кадр.
    /// Получает текущую физическую симуляцию и запускает PlayerMovementJob.
    /// </summary>
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        PhysicsWorldSingleton physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        CollisionWorld collisionWorld = physicsWorld.CollisionWorld;

        Dependency = new PlayerMovementJob
        {
            DeltaTime = deltaTime,
            CollisionWorld = collisionWorld
        }.ScheduleParallel(Dependency);
    }

    /// <summary>
    /// Burst-компилируемая Job, выполняющая логику движения игрока.
    /// Включает проверку заземления, расчет вертикальной скорости (прыжки/падение)
    /// и расчет горизонтальной скорости.
    /// </summary>
    [BurstCompile]
    partial struct PlayerMovementJob : IJobEntity
    {
        /// <summary>
        /// Дельта-время текущего кадра.
        /// </summary>
        public float DeltaTime;

        /// <summary>
        /// Мир физических столкновений для выполнения запросов.
        /// </summary>
        [ReadOnly] public CollisionWorld CollisionWorld;

        /// <summary>
        /// Метод, выполняемый для каждой сущности, соответствующей запросу Job'а.
        /// Обновляет LocalTransform, PhysicsVelocity, PhysicsDamping, PlayerGroundedState
        /// и PlayerStateData на основе входных данных InputsData и конфигурации PlayerControllerData.
        /// </summary>
        /// <param name="entity">Сущность игрока.</param>
        /// <param name="playerTransform">LocalTransform сущности игрока.</param>
        /// <param name="velocity">PhysicsVelocity сущности игрока.</param>
        /// <param name="damping">PhysicsDamping сущности игрока.</param>
        /// <param name="groundedState">PlayerGroundedState сущности игрока.</param>
        /// <param name="stateData">PlayerStateData сущности игрока.</param>
        /// <param name="controllerData">PlayerControllerData сущности игрока.</param>
        /// <param name="groundCheckData">PlayerGroundCheckData сущности игрока.</param>
        /// <param name="inputs">InputsData сущности игрока.</param>
        public void Execute(
            Entity entity,
            ref LocalTransform playerTransform,
            ref PhysicsVelocity velocity,
            ref PhysicsDamping damping,
            ref PlayerGroundedState groundedState,
            ref PlayerStateData stateData,
            in PlayerControllerData controllerData,
            in PlayerGroundCheckData groundCheckData,
            in InputsData inputs)
        {
            float3 sphereCenter = playerTransform.Position + new float3(0f, controllerData.GroundedOffset, 0f);
            NativeList<DistanceHit> hits = new NativeList<DistanceHit>(Allocator.Temp);

            bool isOverlapping = CollisionWorld.OverlapSphere(
                sphereCenter,
                controllerData.GroundedRadius,
                ref hits,
                new CollisionFilter
                {
                    BelongsTo = (uint)(1 << 0),
                    CollidesWith = (uint)controllerData.GroundLayers,
                    GroupIndex = 0
                }
            );

            bool isGrounded = false;
            if (isOverlapping)
            {
                for (int i = 0; i < hits.Length; i++)
                {
                    var hit = hits[i];
                    if (hit.Entity != entity && math.dot(hit.SurfaceNormal, math.up()) > 0.7f)
                    {
                        isGrounded = true;
                        break;
                    }
                }
            }
            hits.Dispose();

            groundedState.IsGrounded = isGrounded;
            stateData.isGrounded = isGrounded;

            if (stateData.jumpTimeoutDelta > 0f) stateData.jumpTimeoutDelta -= DeltaTime;
            if (stateData.fallTimeoutDelta > 0f) stateData.fallTimeoutDelta -= DeltaTime;

            if (isGrounded)
            {
                stateData.jumpTimeoutDelta = 0f;
                stateData.fallTimeoutDelta = controllerData.FallTimeout;

                if (stateData.verticalVelocity < 0.0f)
                    stateData.verticalVelocity = -0.5f;

                if (inputs.jump && stateData.jumpTimeoutDelta <= 0.0f)
                {
                    stateData.verticalVelocity = math.sqrt(controllerData.JumpHeight * -2f * controllerData.Gravity);
                    stateData.jumpTimeoutDelta = controllerData.JumpTimeout;
                }
            }
            else
            {
                if (inputs.jump && stateData.fallTimeoutDelta > 0f)
                {
                    stateData.verticalVelocity = math.sqrt(controllerData.JumpHeight * -2f * controllerData.Gravity);
                    stateData.fallTimeoutDelta = 0f;
                }

                stateData.verticalVelocity += controllerData.Gravity * DeltaTime;
                if (stateData.verticalVelocity < controllerData.TerminalVelocity)
                    stateData.verticalVelocity = controllerData.TerminalVelocity;
            }

            float targetSpeed = inputs.move.Equals(float2.zero)
                ? 0f
                : (inputs.sprint ? controllerData.SprintSpeed : controllerData.MoveSpeed);

            float inputMagnitude = inputs.analogMovement ? math.length(inputs.move) : 1f;
            targetSpeed *= inputMagnitude;

            float speedChangeRate = controllerData.SpeedChangeRate * (isGrounded ? 1f : 0.5f);
            stateData.currentSpeed = math.lerp(stateData.currentSpeed, targetSpeed, DeltaTime * speedChangeRate);
            if (math.abs(stateData.currentSpeed - targetSpeed) < 0.1f)
                stateData.currentSpeed = targetSpeed;

            float3 moveDirection = float3.zero;
            if (!inputs.move.Equals(float2.zero))
            {
                moveDirection = math.normalize(math.mul(
                    playerTransform.Rotation,
                    new float3(inputs.move.x, 0f, inputs.move.y)
                ));
            }

            float3 desiredHorizontalVelocity = moveDirection * stateData.currentSpeed;
            float3 currentHorizontalVelocity = new float3(velocity.Linear.x, 0f, velocity.Linear.z);
            float3 blendedHorizontalVelocity = math.lerp(
                currentHorizontalVelocity,
                desiredHorizontalVelocity,
                DeltaTime * speedChangeRate
            );

            damping.Linear = isGrounded ? controllerData.GroundDamping : controllerData.AirDamping;
            velocity.Linear = new float3(
                blendedHorizontalVelocity.x,
                stateData.verticalVelocity,
                blendedHorizontalVelocity.z
            );
            velocity.Angular = float3.zero;
        }
    }
}