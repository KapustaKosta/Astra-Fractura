using Unity.Entities;
using Unity.Transforms;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Burst;
using UnityEngine;

/// <summary>
/// Система, которая настраивает PhysicsCollider для сущностей превью зданий,
/// делая их "ghost" коллайдерами, не взаимодействующими с физическим миром,
/// и удаляет компоненты, связанные с физической динамикой.
/// </summary>
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BuildingPreviewLifecycleSystem))] 
public partial struct BuildingPreviewSetupSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = SystemAPI
            .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);

        var settings = SystemAPI.GetSingleton<BuildingSettings>();
        int layer = settings.PreviewLayer;
        if (layer < 0 || layer > 31) return; // Если слой не настроен, выходим

        // Запрос только тех сущностей превью, которые помечены NeedsPreviewSetupTag.
        foreach (var (pc, entity) in
                 SystemAPI.Query<RefRW<PhysicsCollider>>()
                          .WithAll<BuildingPreviewTag, NeedsPreviewSetupTag>()
                          .WithEntityAccess())
        {
            // 1. Делаем коллайдер уникальным, если он шарится (для возможности модификации).
            if (!pc.ValueRO.IsUnique)
                pc.ValueRW.MakeUnique(entity, ecb);

            // 2. Настраиваем политику реакции на столкновения: "None" делает его "ghost" коллайдером.
            pc.ValueRW.Value.Value.SetCollisionResponse(
                CollisionResponsePolicy.None);

            // 3. Устанавливаем фильтр столкновений: коллайдер будет принадлежать слою "BuildingPreview"
            // и не будет сталкиваться ни с чем (CollidesWith = 0u).
            pc.ValueRW.Value.Value.SetCollisionFilter(new CollisionFilter
            {
                BelongsTo    = (uint)(1 << layer),
                CollidesWith = 0u,
                GroupIndex   = 0
            });

            // 4. Удаляем компоненты, связанные с физической динамикой, так как превью не должно двигаться
            // под действием физики или иметь массу.
            ecb.RemoveComponent<PhysicsMass>(entity);
            ecb.RemoveComponent<PhysicsVelocity>(entity);
            ecb.RemoveComponent<PhysicsDamping>(entity);
            ecb.RemoveComponent<PhysicsGravityFactor>(entity);

            // 5. Удаляем NeedsPreviewSetupTag, чтобы этот коллайдер больше не обрабатывался данной системой.
            ecb.RemoveComponent<NeedsPreviewSetupTag>(entity);
        }
    }
}