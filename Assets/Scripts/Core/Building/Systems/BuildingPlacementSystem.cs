using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Система, которая позиционирует превью здания в мире и проверяет валидность его размещения.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BuildingPreviewLifecycleSystem))]
public partial class BuildingPlacementSystem : SystemBase
{
    /// <summary>
    /// Вызывается при создании системы. Указывает компоненты, необходимые для обновления системы.
    /// </summary>
    protected override void OnCreate()
    {
        RequireForUpdate<PhysicsWorldSingleton>();
        RequireForUpdate<BuildingPreviewTag>(); 
        RequireForUpdate<BuildingSettings>();
    }

    /// <summary>
    /// Вызывается каждый кадр для обновления логики системы.
    /// Позиционирует превью здания в мире в зависимости от положения мыши,
    /// проверяет валидность размещения (наклон поверхности, отсутствие пересечений с препятствиями)
    /// и добавляет соответствующие теги (PlacementValidTag или PlacementInvalidTag) к сущности превью.
    /// </summary>
    protected override void OnUpdate()
    {
        // Прерываем выполнение, если нет сущности превью здания.
        if (!SystemAPI.TryGetSingletonEntity<BuildingPreviewTag>(out var previewEntity)) return;
        if (!SystemAPI.Exists(previewEntity)) return;

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        var settings = SystemAPI.GetSingleton<BuildingSettings>();

        bool isPlacementValid = false;
        
        // Если у превью есть SnapToEndpointTag — не обновляем позицию по мышке
        if (SystemAPI.HasComponent<SnapToEndpointTag>(previewEntity))
        {
            // Просто используем текущую позицию превью для проверки валидности
            var currentTransform = SystemAPI.GetComponent<LocalTransform>(previewEntity);
            float3 hitPosition = currentTransform.Position;
            float maxPlacementSlopeAngle = settings.MaxPlacementSlopeAngle;
            bool slopeOk = true;
            bool noOverlap = true;
            bool allBottomSupported = true;
            if (SystemAPI.HasComponent<PhysicsCollider>(previewEntity))
            {
                var collider = SystemAPI.GetComponent<PhysicsCollider>(previewEntity);
                var aabb = collider.Value.Value.CalculateAabb(new RigidTransform(Unity.Mathematics.quaternion.identity, hitPosition));
                uint obstacleLayerMask = (uint)settings.ObstacleLayerMask;
                var overlapInput = new OverlapAabbInput
                {
                    Aabb = aabb,
                    Filter = new CollisionFilter { BelongsTo = ~0u, CollidesWith = obstacleLayerMask, GroupIndex = 0 }
                };
                var overlappingBodies = new NativeList<int>(Allocator.Temp);
                noOverlap = !physicsWorld.CollisionWorld.OverlapAabb(overlapInput, ref overlappingBodies);
                overlappingBodies.Dispose();

                float3 min = aabb.Min;
                float3 max = aabb.Max;
                float y = min.y + 0.01f;
                float3[] bottomPoints = new float3[5];
                bottomPoints[0] = new float3(min.x, y, min.z);
                bottomPoints[1] = new float3(max.x, y, min.z);
                bottomPoints[2] = new float3(min.x, y, max.z);
                bottomPoints[3] = new float3(max.x, y, max.z);
                bottomPoints[4] = new float3((min.x+max.x)*0.5f, y, (min.z+max.z)*0.5f);

                float checkDepth = 0.6f;
                float upCheck = 1f;
                for (int i = 0; i < bottomPoints.Length; i++)
                {
                    var downRay = new RaycastInput
                    {
                        Start = bottomPoints[i],
                        End = bottomPoints[i] + new float3(0, -checkDepth, 0),
                        Filter = new CollisionFilter { BelongsTo = ~0u, CollidesWith = (uint)settings.BuildableSurfaceLayerMask, GroupIndex = 0 }
                    };
                    if (!physicsWorld.CollisionWorld.CastRay(downRay, out var _))
                    {
                        allBottomSupported = false;
                        break;
                    }
                    var upRay = new RaycastInput
                    {
                        Start = bottomPoints[i],
                        End = bottomPoints[i] + new float3(0, upCheck, 0),
                        Filter = new CollisionFilter { BelongsTo = ~0u, CollidesWith = (uint)settings.BuildableSurfaceLayerMask, GroupIndex = 0 }
                    };
                    if (physicsWorld.CollisionWorld.CastRay(upRay, out var _))
                    {
                        allBottomSupported = false;
                        break;
                    }
                }
            }
            isPlacementValid = slopeOk && noOverlap && allBottomSupported;
        }
        else if (Camera.main != null)
        {
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            // Используем настроенные маски слоев для рейкаста.
            uint buildableSurfaceLayerMask = (uint)settings.BuildableSurfaceLayerMask;
            uint obstacleLayerMask = (uint)settings.ObstacleLayerMask;

            var rayInput = new RaycastInput
            {
                Start = ray.origin,
                End = ray.origin + ray.direction * settings.MaxPlacementDistance,
                Filter = new CollisionFilter { BelongsTo = ~0u, CollidesWith = buildableSurfaceLayerMask, GroupIndex = 0 }
            };

            if (physicsWorld.CollisionWorld.CastRay(rayInput, out var hit))
            {
                // Сохраняем rotation и scale при обновлении позиции превью
                var currentTransform = SystemAPI.GetComponent<LocalTransform>(previewEntity);
                SystemAPI.SetComponent(previewEntity, LocalTransform.FromPositionRotationScale(
                    hit.Position,
                    currentTransform.Rotation,
                    currentTransform.Scale
                ));

                // Расчет угла наклона поверхности и проверка на допустимость.
                float maxPlacementSlopeAngle = settings.MaxPlacementSlopeAngle;
                float slope = math.degrees(math.acos(math.dot(new float3(0, 1, 0), hit.SurfaceNormal)));
                bool slopeOk = slope <= maxPlacementSlopeAngle;

                bool noOverlap = true;
                bool allBottomSupported = true;
                if (SystemAPI.HasComponent<PhysicsCollider>(previewEntity))
                {
                    var collider = SystemAPI.GetComponent<PhysicsCollider>(previewEntity);
                    var aabb = collider.Value.Value.CalculateAabb(new RigidTransform(Unity.Mathematics.quaternion.identity, hit.Position));
                    // Проверка на пересечение AABB превью с препятствиями.
                    var overlapInput = new OverlapAabbInput
                    {
                        Aabb = aabb,
                        Filter = new CollisionFilter { BelongsTo = ~0u, CollidesWith = obstacleLayerMask, GroupIndex = 0 }
                    };
                    var overlappingBodies = new NativeList<int>(Allocator.Temp);
                    noOverlap = !physicsWorld.CollisionWorld.OverlapAabb(overlapInput, ref overlappingBodies);
                    overlappingBodies.Dispose();

                    // Проверка: точки дна берём напрямую из AABB (world space), offset не нужен
                    float3 min = aabb.Min;
                    float3 max = aabb.Max;
                    float y = min.y + 0.01f; // чуть выше дна, чтобы гарантировать попадание
                    float3[] bottomPoints = new float3[5];
                    bottomPoints[0] = new float3(min.x, y, min.z);
                    bottomPoints[1] = new float3(max.x, y, min.z);
                    bottomPoints[2] = new float3(min.x, y, max.z);
                    bottomPoints[3] = new float3(max.x, y, max.z);
                    bottomPoints[4] = new float3((min.x+max.x)*0.5f, y, (min.z+max.z)*0.5f);

                    float checkDepth = 0.6f;
                    float upCheck = 1f; // высота проверки вверх
                    for (int i = 0; i < bottomPoints.Length; i++)
                    {
                        // Проверка: есть ли поверхность под точкой
                        var downRay = new RaycastInput
                        {
                            Start = bottomPoints[i],
                            End = bottomPoints[i] + new float3(0, -checkDepth, 0),
                            Filter = new CollisionFilter { BelongsTo = ~0u, CollidesWith = buildableSurfaceLayerMask, GroupIndex = 0 }
                        };
                        if (!physicsWorld.CollisionWorld.CastRay(downRay, out var _))
                        {
                            allBottomSupported = false;
                            break;
                        }
                        // Проверка: не находится ли точка уже внутри поверхности (например, угол утоплен)
                        var upRay = new RaycastInput
                        {
                            Start = bottomPoints[i],
                            End = bottomPoints[i] + new float3(0, upCheck, 0),
                            Filter = new CollisionFilter { BelongsTo = ~0u, CollidesWith = buildableSurfaceLayerMask, GroupIndex = 0 }
                        };
                        if (physicsWorld.CollisionWorld.CastRay(upRay, out var _))
                        {
                            allBottomSupported = false;
                            break;
                        }
                    }
                }

                isPlacementValid = slopeOk && noOverlap && allBottomSupported;
            }
        }
        
        // Обновляем теги валидности размещения на сущности превью.
        if (SystemAPI.Exists(previewEntity))
        {
            if (isPlacementValid)
            {
                ecb.AddComponent<PlacementValidTag>(previewEntity);
                ecb.RemoveComponent<PlacementInvalidTag>(previewEntity);
            }
            else
            {
                ecb.AddComponent<PlacementInvalidTag>(previewEntity);
                ecb.RemoveComponent<PlacementValidTag>(previewEntity);
            }
        }
    }
}