using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Conveyor
{
    /// <summary>
    /// Применяет рассчитанные позы (позиция/поворот/длина) к пулу гост-секций.
    /// Эта система ТОЛЬКО читает буферы поз и применяет их к сущностям,
    /// не производя собственных расчетов геометрии.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ConveyorValidationSystem))] // Запускаемся после валидации
    public partial class ConveyorGhostApplyPosesSystem : SystemBase
    {
        private static readonly float4 TintFrozen = new float4(0.20f, 0.80f, 1.00f, 0.35f);
        private static readonly float4 TintLiveValid = new float4(0.20f, 1.00f, 0.60f, 0.50f); // Зеленый
        private static readonly float4 TintLiveInvalid = new float4(1.00f, 0.20f, 0.20f, 0.45f); // Красный

        protected override void OnUpdate()
        {
            var em = EntityManager;

            if (!SystemAPI.TryGetSingletonEntity<GameState>(out var gs)) return;
            if (!SystemAPI.HasComponent<InConveyorMode>(gs)) return;

            var st = SystemAPI.GetComponent<ConveyorState>(gs);
            var holder = st.PreviewEntity;

            if (holder == Entity.Null || !em.Exists(holder)) return;

            if (!em.HasBuffer<ConveyorFrozenPose>(holder) || !em.HasBuffer<ConveyorLivePose>(holder) ||
                !em.HasBuffer<ConveyorGhostFrozenRef>(holder) || !em.HasBuffer<ConveyorGhostLiveRef>(holder))
            {
                return;
            }

            var frozenPoses = em.GetBuffer<ConveyorFrozenPose>(holder).ToNativeArray(Allocator.Temp);
            var livePoses = em.GetBuffer<ConveyorLivePose>(holder).ToNativeArray(Allocator.Temp);
            var frozenPoolRefs = em.GetBuffer<ConveyorGhostFrozenRef>(holder).ToNativeArray(Allocator.Temp);
            var livePoolRefs = em.GetBuffer<ConveyorGhostLiveRef>(holder).ToNativeArray(Allocator.Temp);

            var prefab = ItemToEntityResolver.GetEntityPrefabFromID(em, st.ItemID);
            if (prefab == Entity.Null)
            {
                for (int i = 0; i < frozenPoolRefs.Length; i++)
                    ConveyorGhostUtil.DisableRecursive(em, frozenPoolRefs[i].Value);
                for (int i = 0; i < livePoolRefs.Length; i++)
                    ConveyorGhostUtil.DisableRecursive(em, livePoolRefs[i].Value);
            }
            else
            {
                float baseLen = 6f;
                if (em.HasComponent<ConveyorSegmentSettings>(prefab))
                {
                    var settings = em.GetComponentData<ConveyorSegmentSettings>(prefab);
                    if (settings.Length > 0) baseLen = settings.Length;
                }

                // Применяем "замороженные" позы
                for (int i = 0; i < frozenPoolRefs.Length; i++)
                {
                    var ghostEntity = frozenPoolRefs[i].Value;
                    if (i < frozenPoses.Length)
                    {
                        var pose = frozenPoses[i];
                        ConveyorGhostUtil.EnableRecursive(em, ghostEntity);
                        ConveyorGhostUtil.ApplyPoseWithLength(em, ghostEntity, pose.Position, pose.Rotation, pose.Length, baseLen);
                        ConveyorGhostUtil.SetTintRecursive(em, ghostEntity, TintFrozen);
                    }
                    else
                    {
                        ConveyorGhostUtil.DisableRecursive(em, ghostEntity);
                    }
                }

                bool isPlacementValid = em.HasComponent<ConveyorPlacementValidTag>(holder);
                var liveColor = isPlacementValid ? TintLiveValid : TintLiveInvalid;


                // Применяем "живые" позы
                for (int i = 0; i < livePoolRefs.Length; i++)
                {
                    var ghostEntity = livePoolRefs[i].Value;
                    if (i < livePoses.Length)
                    {
                        var pose = livePoses[i];
                        ConveyorGhostUtil.EnableRecursive(em, ghostEntity);
                        ConveyorGhostUtil.ApplyPoseWithLength(em, ghostEntity, pose.Position, pose.Rotation, pose.Length, baseLen);
                        ConveyorGhostUtil.SetTintRecursive(em, ghostEntity, liveColor);
                    }
                    else
                    {
                        ConveyorGhostUtil.DisableRecursive(em, ghostEntity);
                    }
                }
            }

            frozenPoses.Dispose();
            livePoses.Dispose();
            frozenPoolRefs.Dispose();
            livePoolRefs.Dispose();
        }
    }
}