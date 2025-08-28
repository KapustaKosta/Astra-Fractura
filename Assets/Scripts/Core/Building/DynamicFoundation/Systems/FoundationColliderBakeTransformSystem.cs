using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;   // MakeUnique, BakeTransform
using Unity.Physics.Systems;      // PhysicsSystemGroup, PhysicsInitializeGroup
using Unity.Transforms;


[UpdateInGroup(typeof(PhysicsSystemGroup))]
[UpdateBefore(typeof(PhysicsInitializeGroup))] // успеваем до построения мира физики
public partial struct FoundationColliderBakeTransformSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        // Обновляемся только когда есть что запекать
        state.RequireForUpdate<FoundationColliderScale>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var em = state.EntityManager;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        var targets = new NativeList<Entity>(Allocator.Temp);
        var scales = new NativeList<float>(Allocator.Temp);

        foreach (var (scaleRO, e) in SystemAPI.Query<RefRO<FoundationColliderScale>>().WithEntityAccess())
        {
            targets.Add(e);
            scales.Add(math.max(1e-4f, scaleRO.ValueRO.Y));
        }

        for (int i = 0; i < targets.Length; i++)
        {
            var ent = targets[i];
            float yScl = scales[i];
            var bake = new AffineTransform(float3.zero, quaternion.identity, new float3(1f, yScl, 1f));

            BakeOnEntity(em, ent, bake);

            // Запекаем и на дочерних (если коллайдеры не на корне)
            if (em.HasBuffer<LinkedEntityGroup>(ent))
            {
                var group = em.GetBuffer<LinkedEntityGroup>(ent);
                for (int gi = 0; gi < group.Length; gi++)
                {
                    var child = group[gi].Value;
                    if (child != ent) BakeOnEntity(em, child, bake);
                }
            }

            // Снимаем маркер — одноразовая операция
            ecb.RemoveComponent<FoundationColliderScale>(ent);
        }

        ecb.Playback(em);
        ecb.Dispose();
        targets.Dispose();
        scales.Dispose();
    }

    private static void BakeOnEntity(EntityManager em, Entity ent, AffineTransform bake)
    {
        if (!em.HasComponent<PhysicsCollider>(ent)) return;

        var pc = em.GetComponentData<PhysicsCollider>(ent);
        if (!pc.IsValid || !pc.Value.IsCreated) return;

        // Перед модификацией делаем Blob уникальным
        if (!pc.IsUnique)
            pc.MakeUnique(ent, em);

        ref var collider = ref pc.Value.Value;
        collider.BakeTransform(bake);

        em.SetComponentData(ent, pc);
    }
}
