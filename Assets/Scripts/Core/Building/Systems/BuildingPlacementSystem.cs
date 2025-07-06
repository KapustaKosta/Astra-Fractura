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
        
        if (Camera.main != null)
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
                SystemAPI.SetComponent(previewEntity, LocalTransform.FromPosition(hit.Position));

                // Расчет угла наклона поверхности и проверка на допустимость.
                float maxPlacementSlopeAngle = settings.MaxPlacementSlopeAngle;
                float slope = math.degrees(math.acos(math.dot(new float3(0, 1, 0), hit.SurfaceNormal)));
                bool slopeOk = slope <= maxPlacementSlopeAngle;

                bool noOverlap = true;
                if (SystemAPI.HasComponent<PhysicsCollider>(previewEntity))
                {
                    var collider = SystemAPI.GetComponent<PhysicsCollider>(previewEntity);
                    
                    // Проверка на пересечение AABB превью с препятствиями.
                    var overlapInput = new OverlapAabbInput
                    {
                        Aabb = collider.Value.Value.CalculateAabb(new RigidTransform(Unity.Mathematics.quaternion.identity, hit.Position)),
                        Filter = new CollisionFilter { BelongsTo = ~0u, CollidesWith = obstacleLayerMask, GroupIndex = 0 }
                    };
                    
                    var overlappingBodies = new NativeList<int>(Allocator.Temp);
                    noOverlap = !physicsWorld.CollisionWorld.OverlapAabb(overlapInput, ref overlappingBodies);
                    overlappingBodies.Dispose();
                }

                isPlacementValid = slopeOk && noOverlap;
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