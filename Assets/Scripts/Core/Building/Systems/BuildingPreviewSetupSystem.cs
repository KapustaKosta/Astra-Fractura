using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;

/// <summary>
/// Система, которая настраивает PhysicsCollider для сущностей превью зданий,
/// делая их "ghost" коллайдерами, не взаимодействующими с физическим миром,
/// и удаляет компоненты, связанные с физической динамикой.
/// </summary>
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct BuildingPreviewSetupSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.HasSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()) return;
        var ecb = SystemAPI
            .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (ltw, previewRoot) in SystemAPI
                     .Query<RefRO<LocalToWorld>>()
                     .WithAll<BuildingPreviewTag, NeedsPreviewSetupTag>()
                     .WithEntityAccess())
        {
            var toProcess = new NativeList<Entity>(Allocator.Temp);

            if (SystemAPI.HasBuffer<LinkedEntityGroup>(previewRoot))
            {
                var leg = SystemAPI.GetBuffer<LinkedEntityGroup>(previewRoot);
                for (int i = 0; i < leg.Length; i++) toProcess.Add(leg[i].Value);
            }
            else
            {
                toProcess.Add(previewRoot);
                if (SystemAPI.HasBuffer<Child>(previewRoot))
                {
                    var ch = SystemAPI.GetBuffer<Child>(previewRoot);
                    for (int i = 0; i < ch.Length; i++) toProcess.Add(ch[i].Value);
                }
            }

            for (int i = 0; i < toProcess.Length; i++)
            {
                var e = toProcess[i];
                if (!SystemAPI.HasComponent<PhysicsCollider>(e)) continue;

                // 🔸 СДЕЛАТЬ КОЛЛАЙДЕР УНИКАЛЬНЫМ ДЛЯ ПРЕВЬЮ
                var pc = SystemAPI.GetComponentRW<PhysicsCollider>(e);
                MakeColliderUnique(ref pc.ValueRW);

                // ⬇ теперь меняем фильтр УЖЕ на копии — не задевая оригинальные префабы
                var col = pc.ValueRW.Value;
                var filter = col.Value.GetCollisionFilter();
                filter.CollidesWith = 0u; // ни с кем не сталкиваться
                col.Value.SetCollisionFilter(filter);
                col.Value.SetCollisionResponse(CollisionResponsePolicy.None);
                pc.ValueRW.Value = col;

                // Убираем динамику, чтобы превью не "жило" в мире
                if (SystemAPI.HasComponent<PhysicsMass>(e)) ecb.RemoveComponent<PhysicsMass>(e);
                if (SystemAPI.HasComponent<PhysicsVelocity>(e)) ecb.RemoveComponent<PhysicsVelocity>(e);
                if (SystemAPI.HasComponent<PhysicsDamping>(e)) ecb.RemoveComponent<PhysicsDamping>(e);
                if (SystemAPI.HasComponent<PhysicsGravityFactor>(e)) ecb.RemoveComponent<PhysicsGravityFactor>(e);
            }

            toProcess.Dispose();
            ecb.RemoveComponent<NeedsPreviewSetupTag>(previewRoot);
        }
    }

    // Делает Blob коллайдера уникальным, чтобы правки не затрагивали другие экземпляры/префабы
    static void MakeColliderUnique(ref PhysicsCollider physicsCollider)
    {
        // Уже уникален — выходим (Unity помечает это флагом IsUnique)
        if (physicsCollider.IsUnique) return;

        // Клонируем Blob (Allocator.Persistent — ок, будет освобождён с компонентом)
        var cloned = physicsCollider.Value.Value.Clone();
        physicsCollider = new PhysicsCollider { Value = cloned };
    }
}
