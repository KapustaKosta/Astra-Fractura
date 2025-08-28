using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Physics;

namespace Conveyor
{
    /// <summary>
    /// Утилиты для безопасной работы с ghost-превью.
    /// </summary>
    public static class ConveyorGhostUtil
    {
        private static NativeList<Entity> CopyLinkedGroup(EntityManager em, Entity root, Allocator allocator)
        {
            var list = new NativeList<Entity>(allocator);
            list.Add(root);

            if (em.HasBuffer<LinkedEntityGroup>(root))
            {
                var leg = em.GetBuffer<LinkedEntityGroup>(root, true);
                for (int i = 0; i < leg.Length; i++)
                {
                    var child = leg[i].Value;
                    if (child != root) list.Add(child);
                }
            }
            return list;
        }

        /// <summary>Пометить всё поддерево тегом призрака.</summary>
        public static void TagAsGhostRecursive(EntityManager em, Entity root)
        {
            var group = CopyLinkedGroup(em, root, Allocator.Temp);
            for (int i = 0; i < group.Length; i++)
            {
                var e = group[i];
                if (!em.HasComponent<ConveyorGhostTag>(e))
                    em.AddComponent<ConveyorGhostTag>(e);
            }
            group.Dispose();
        }

        /// <summary>
        /// Подготовить инстанс как "гост": удалить физ.коллайдеры и гарантировать наличие параметра цвета.
        /// Вызывать ОДИН РАЗ при создании пулов (дорого выполнять каждый кадр).
        /// </summary>
        public static void PrepareGhostInstance(EntityManager em, Entity root, float4 initialTint)
        {
            var group = CopyLinkedGroup(em, root, Allocator.Temp);

            for (int i = 0; i < group.Length; i++)
            {
                var e = group[i];

                // Физика: убрать коллайдер, чтобы госты не толкали игрока и не участвовали в мире
                if (em.HasComponent<PhysicsCollider>(e))
                    em.RemoveComponent<PhysicsCollider>(e);

                // Материал: гарантировать наличие URPMaterialPropertyBaseColor
                if (!em.HasComponent<URPMaterialPropertyBaseColor>(e))
                    em.AddComponentData(e, new URPMaterialPropertyBaseColor { Value = initialTint });
                else
                    em.SetComponentData(e, new URPMaterialPropertyBaseColor { Value = initialTint });
            }

            group.Dispose();
        }

        public static void DisableRecursive(EntityManager em, Entity root)
        {
            var group = CopyLinkedGroup(em, root, Allocator.Temp);
            for (int i = 0; i < group.Length; i++)
            {
                var e = group[i];
                if (!em.HasComponent<Disabled>(e))
                    em.AddComponent<Disabled>(e);
            }
            group.Dispose();
        }

        public static void EnableRecursive(EntityManager em, Entity root)
        {
            var group = CopyLinkedGroup(em, root, Allocator.Temp);
            for (int i = 0; i < group.Length; i++)
            {
                var e = group[i];
                if (em.HasComponent<Disabled>(e))
                    em.RemoveComponent<Disabled>(e);
            }
            group.Dispose();
        }

        /// <summary>
        /// Применить позу и неуниформный масштаб по Z (совпадает с финализацией).
        /// </summary>
        public static void ApplyPoseWithLength(EntityManager em, Entity root, float3 pos, quaternion rot, float length, float baseLen)
        {
            if (!em.HasComponent<LocalTransform>(root))
                em.AddComponentData(root, LocalTransform.FromPositionRotationScale(pos, rot, 1f));
            else
            {
                var lt = em.GetComponentData<LocalTransform>(root);
                lt.Position = pos;
                lt.Rotation = rot;
                lt.Scale = 1f;
                em.SetComponentData(root, lt);
            }

            // ВНИМАНИЕ: теперь масштабируем ИМЕННО ПО Z (как в финальной постройке)
            float sz = math.max(0.0001f, baseLen <= 0 ? 1f : (length / baseLen));
            var scale = float4x4.Scale(new float3(1f, 1f, sz));

            if (!em.HasComponent<PostTransformMatrix>(root))
                em.AddComponentData(root, new PostTransformMatrix { Value = scale });
            else
                em.SetComponentData(root, new PostTransformMatrix { Value = scale });
        }

        /// <summary>Покрасить всё поддерево, если есть URPMaterialPropertyBaseColor.</summary>
        public static void SetTintRecursive(EntityManager em, Entity root, float4 rgba)
        {
            var group = CopyLinkedGroup(em, root, Allocator.Temp);
            for (int i = 0; i < group.Length; i++)
            {
                var e = group[i];
                if (em.HasComponent<URPMaterialPropertyBaseColor>(e))
                    em.SetComponentData(e, new URPMaterialPropertyBaseColor { Value = rgba });
            }
            group.Dispose();
        }
    }
}
