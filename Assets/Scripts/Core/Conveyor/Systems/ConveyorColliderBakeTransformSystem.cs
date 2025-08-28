using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions; 
using Unity.Physics.Systems;
using Unity.Transforms;

namespace Conveyor
{
    // Выполняем ДО BuildPhysicsWorld: чтобы запекание попало в текущий кадр
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateBefore(typeof(PhysicsInitializeGroup))]
    public partial struct ConveyorColliderBakeTransformSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ConveyorSegmentScale>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // 1) Сначала — просто собираем цели (никаких изменений в этот момент)
            var targets = new NativeList<Entity>(Allocator.Temp);
            var scales = new NativeList<float>(Allocator.Temp);

            foreach (var (scaleRO, e) in SystemAPI.Query<RefRO<ConveyorSegmentScale>>().WithEntityAccess())
            {
                targets.Add(e);
                scales.Add(math.max(1e-4f, scaleRO.ValueRO.Z));
            }

            // 2) Теперь можно безопасно вносить структурные изменения (мы больше не итерируем Query)
            for (int i = 0; i < targets.Length; i++)
            {
                var e = targets[i];
                var bake = new AffineTransform(float3.zero, quaternion.identity, new float3(1f, 1f, scales[i]));

                // корневой энтити
                BakeOnEntity(em, e, bake);

                // и все дочерние/связанные (PhysicsShape часто сидит не на корне)
                if (em.HasBuffer<LinkedEntityGroup>(e))
                {
                    var group = em.GetBuffer<LinkedEntityGroup>(e);
                    for (int gi = 0; gi < group.Length; gi++)
                    {
                        var child = group[gi].Value;
                        if (child != e) BakeOnEntity(em, child, bake);
                    }
                }

                // снятие маркёра — через ECB (структурное изменение)
                ecb.RemoveComponent<ConveyorSegmentScale>(e);
            }

            ecb.Playback(em);
            ecb.Dispose();
            targets.Dispose();
            scales.Dispose();
        }

        /// <summary>Делает коллайдер уникальным (если надо) и запекает в него аффинный трансформ.</summary>
        private static void BakeOnEntity(EntityManager em, Entity ent, AffineTransform bake)
        {
            if (!em.HasComponent<PhysicsCollider>(ent)) return;

            var pc = em.GetComponentData<PhysicsCollider>(ent);
            if (!pc.IsValid || !pc.Value.IsCreated) return;

            // Важно: перед модификацией обеспечить уникальность (иначе правим шаренный Blob)
            if (!pc.IsUnique)
                pc.MakeUnique(ent, em); // это структурное изменение, но здесь мы уже НЕ итерируем Query

            // Запекаем масштаб/поворот/сдвиг в геометрию (работает и для Box/Convex/Compound/Mesh)
            ref var collider = ref pc.Value.Value;
            collider.BakeTransform(bake);

            em.SetComponentData(ent, pc);
        }
    }
}
