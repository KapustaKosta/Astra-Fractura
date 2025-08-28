using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Conveyor
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ConveyorPreviewPoseComputeSystem))]
    public partial class ConveyorGhostPoolResizeSystem : SystemBase
    {
        // Базовые тиныт на "создание" (потом ApplyPoses перекрасит в Frozen/Live каждый кадр)
        private static readonly float4 InitialTint = new float4(0.5f, 0.9f, 1f, 0.4f);

        protected override void OnUpdate()
        {
            var em = EntityManager;

            if (!SystemAPI.TryGetSingletonEntity<GameState>(out var gs)) return;
            if (!em.HasComponent<InConveyorMode>(gs)) return;
            if (!em.HasComponent<ConveyorState>(gs)) return;

            var st = em.GetComponentData<ConveyorState>(gs);
            var holder = st.PreviewEntity;
            if (holder == Entity.Null || !em.Exists(holder)) return;

            if (!em.HasBuffer<ConveyorFrozenPose>(holder) || !em.HasBuffer<ConveyorLivePose>(holder))
                return;

            var desiredFrozenCount = em.GetBuffer<ConveyorFrozenPose>(holder).Length;
            var desiredLiveCount = em.GetBuffer<ConveyorLivePose>(holder).Length;

            if (!em.HasBuffer<ConveyorGhostFrozenRef>(holder)) em.AddBuffer<ConveyorGhostFrozenRef>(holder);
            if (!em.HasBuffer<ConveyorGhostLiveRef>(holder)) em.AddBuffer<ConveyorGhostLiveRef>(holder);

            var prefab = ItemToEntityResolver.GetEntityPrefabFromID(em, st.ItemID);
            if (prefab == Entity.Null) return;

            ResizeFrozenPool(em, prefab, holder, desiredFrozenCount);
            ResizeLivePool(em, prefab, holder, desiredLiveCount);
        }

        private static void ResizeFrozenPool(EntityManager em, Entity prefab, Entity holder, int desiredCount)
        {
            var pool = em.GetBuffer<ConveyorGhostFrozenRef>(holder);
            int cur = pool.Length;

            if (cur > desiredCount)
            {
                var toDestroy = new NativeArray<Entity>(cur - desiredCount, Allocator.Temp);
                for (int i = 0; i < toDestroy.Length; i++) toDestroy[i] = pool[desiredCount + i].Value;
                em.DestroyEntity(toDestroy);
                toDestroy.Dispose();
                em.GetBuffer<ConveyorGhostFrozenRef>(holder).RemoveRange(desiredCount, cur - desiredCount);
            }

            if (cur < desiredCount)
            {
                int toCreate = desiredCount - cur;
                var newInstances = new NativeArray<Entity>(toCreate, Allocator.Temp);
                em.Instantiate(prefab, newInstances);

                var newRefs = new NativeArray<ConveyorGhostFrozenRef>(toCreate, Allocator.Temp);
                for (int i = 0; i < toCreate; i++)
                {
                    var inst = newInstances[i];
                    ConveyorGhostUtil.TagAsGhostRecursive(em, inst);
                    // ключевая подготовка: убрать физику и добавить цветовой параметр
                    ConveyorGhostUtil.PrepareGhostInstance(em, inst, InitialTint);

                    // изначально скрываем до применения поз
                    if (!em.HasComponent<Disabled>(inst))
                        em.AddComponent<Disabled>(inst);

                    newRefs[i] = new ConveyorGhostFrozenRef { Value = inst };
                }
                em.GetBuffer<ConveyorGhostFrozenRef>(holder).AddRange(newRefs);
                newInstances.Dispose();
                newRefs.Dispose();
            }
        }

        private static void ResizeLivePool(EntityManager em, Entity prefab, Entity holder, int desiredCount)
        {
            var pool = em.GetBuffer<ConveyorGhostLiveRef>(holder);
            int cur = pool.Length;

            if (cur > desiredCount)
            {
                var toDestroy = new NativeArray<Entity>(cur - desiredCount, Allocator.Temp);
                for (int i = 0; i < toDestroy.Length; i++) toDestroy[i] = pool[desiredCount + i].Value;
                em.DestroyEntity(toDestroy);
                toDestroy.Dispose();
                em.GetBuffer<ConveyorGhostLiveRef>(holder).RemoveRange(desiredCount, cur - desiredCount);
            }

            if (cur < desiredCount)
            {
                int toCreate = desiredCount - cur;
                var newInstances = new NativeArray<Entity>(toCreate, Allocator.Temp);
                em.Instantiate(prefab, newInstances);

                var newRefs = new NativeArray<ConveyorGhostLiveRef>(toCreate, Allocator.Temp);
                for (int i = 0; i < toCreate; i++)
                {
                    var inst = newInstances[i];
                    ConveyorGhostUtil.TagAsGhostRecursive(em, inst);
                    // ключевая подготовка: убрать физику и добавить цветовой параметр
                    ConveyorGhostUtil.PrepareGhostInstance(em, inst, InitialTint);

                    // изначально скрываем до применения поз
                    if (!em.HasComponent<Disabled>(inst))
                        em.AddComponent<Disabled>(inst);

                    newRefs[i] = new ConveyorGhostLiveRef { Value = inst };
                }
                em.GetBuffer<ConveyorGhostLiveRef>(holder).AddRange(newRefs);
                newInstances.Dispose();
                newRefs.Dispose();
            }
        }
    }
}
