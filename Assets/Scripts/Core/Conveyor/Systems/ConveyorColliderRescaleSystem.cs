#define ENABLE_UNITY_COLLECTIONS_CHECKS
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;                
using PhysicsMaterial = Unity.Physics.Material;

namespace Conveyor
{
    // Выполняем после проигрывания EndSimulationECB 
    // чтобы инстанс и его дети уже существовали.
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EndSimulationEntityCommandBufferSystem))]
    public partial class ConveyorColliderRescaleSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<ConveyorSegmentScale>();
        }

        protected override void OnUpdate()
        {
            var em = EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Проходим все сегменты, помеченные к масштабированию
            foreach (var (scale, e) in SystemAPI.Query<RefRO<ConveyorSegmentScale>>().WithEntityAccess())
            {
                float zScale = math.max(1e-4f, scale.ValueRO.Z);

                // 1) Корневая сущность
                TryRescaleBoxColliderOnEntity(em, e, zScale);

                // 2) Все дочерние/связанные сущности инстанса
                if (em.HasBuffer<LinkedEntityGroup>(e))
                {
                    var group = em.GetBuffer<LinkedEntityGroup>(e);
                    for (int i = 0; i < group.Length; i++)
                    {
                        var child = group[i].Value;
                        if (child == e) continue;
                        TryRescaleBoxColliderOnEntity(em, child, zScale);
                    }
                }

                // Убираем маркер, чтобы не делать работу каждый кадр
                ecb.RemoveComponent<ConveyorSegmentScale>(e);
            }

            ecb.Playback(em);
            ecb.Dispose();
        }

        /// <summary>Пересобрать BoxCollider на сущности, если он есть.</summary>
        private static unsafe void TryRescaleBoxColliderOnEntity(EntityManager em, Entity ent, float zScale)
        {
            if (!em.HasComponent<PhysicsCollider>(ent)) return;

            var pc = em.GetComponentData<PhysicsCollider>(ent);
            if (!pc.IsValid || !pc.Value.IsCreated) return;

            var col = (Unity.Physics.Collider*)pc.Value.GetUnsafePtr();
            if (col->Type != ColliderType.Box) return; // если Compound/Convex — пропускаем

            var box = (Unity.Physics.BoxCollider*)col;

            // геометрия из префаба/инстанса
            BoxGeometry geom = box->Geometry;
            var size = geom.Size;
            size.z = math.max(1e-4f, size.z * zScale);
            geom.Size = size;

            // подстрахуем скругление
            float minHalf = 0.5f * math.cmin(geom.Size);
            geom.BevelRadius = math.clamp(geom.BevelRadius, 0f, math.max(1e-5f, minHalf));

            CollisionFilter filter = box->GetCollisionFilter();
            PhysicsMaterial mat = box->Material;

            // создаём уникальный Blob и подменяем
            var newCol = Unity.Physics.BoxCollider.Create(geom, filter, mat);
            em.SetComponentData(ent, new PhysicsCollider { Value = newCol });
        }
    }
}
