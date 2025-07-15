using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using UnityEngine;

/// <summary>
/// Система, которая преобразует компонент-запрос MoveToRequest в физическую скорость (PhysicsVelocity).
/// Работает в физическом цикле для корректного взаимодействия с Unity.Physics.
/// </summary>
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))] 
[UpdateBefore(typeof(PhysicsSystemGroup))] 
public partial class NPCMovementSystem : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<AISettings>();
    }
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        float dt = SystemAPI.Time.DeltaTime;
        var settings = SystemAPI.GetSingleton<AISettings>(); // Получаем настройки
        var transformLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);

        // Система работает только с сущностями, у которых есть запрос на движение.
        Entities
            .WithReadOnly(transformLookup)
            .WithAll<MoveToRequest>()
            // Исключаем NPC, которые заняты добычей, чтобы они не пытались двигаться.
            .WithNone<WantsToHarvestTag>() 
            .ForEach((Entity entity, ref PhysicsVelocity velocity, ref LocalTransform transform, in MoveToRequest request, in NPCMovementComponent movement) =>
            {
                // Проверяем, существует ли цель движения.
                if (!transformLookup.HasComponent(request.TargetEntity))
                {
                    ecb.RemoveComponent<MoveToRequest>(entity);
                    velocity.Linear = float3.zero; // Останавливаем NPC, если цель исчезла.
                    return;
                }

                var targetPosition = transformLookup[request.TargetEntity].Position;
                float stoppingDistance = request.StoppingDistance; 
                float distanceSq = math.distancesq(transform.Position, targetPosition);

                // Проверяем, достигли ли мы точки назначения.
                if (distanceSq <= stoppingDistance * stoppingDistance)
                {
                    // Удаляем запрос и полностью обнуляем скорость и вращение.
                    ecb.RemoveComponent<MoveToRequest>(entity);
                    velocity.Linear = float3.zero;
                    velocity.Angular = float3.zero;
                }
                else
                {
                    // Вычисляем направление к цели и передаем скорость физическому движку.
                    // Физический движок сам обработает столкновения.
                    float3 direction = math.normalize(targetPosition - transform.Position);
                    velocity.Linear = direction * movement.Speed;

                    // Поворот остается кинематическим для более плавного визуального результата.
                    if (math.lengthsq(direction) > 0.001f)
                    {
                        quaternion targetRotation = quaternion.LookRotation(direction, math.up());
                        transform.Rotation = math.slerp(transform.Rotation, targetRotation, dt * settings.RotationSpeed); // Используем настройку
                    }
                }
            })
            .Schedule();
    }
}