using Unity.Entities;
using UnityEngine;

namespace Conveyor
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(ConveyorPreviewPoseComputeSystem))]
    public partial class ConveyorPreviewLifecycleSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var em = EntityManager;
            if (!SystemAPI.TryGetSingletonEntity<GameState>(out var gs)) return;
            bool inConveyorMode = em.HasComponent<InConveyorMode>(gs);

            if (inConveyorMode)
            {
                if (!em.HasComponent<ConveyorState>(gs))
                {

                    em.AddComponentData(gs, new ConveyorState { SnapRadius = 2.5f });
                }
                var st = em.GetComponentData<ConveyorState>(gs);
                if (st.PreviewEntity == Entity.Null || !em.Exists(st.PreviewEntity))
                {
                    var holder = em.CreateEntity();
                    em.AddComponentData(holder, new ConveyorPreviewTag());
                    em.AddBuffer<ConveyorPathPoint>(holder);
                    em.AddBuffer<ConveyorGhostFrozenRef>(holder);
                    em.AddBuffer<ConveyorGhostLiveRef>(holder);
                    em.AddBuffer<ConveyorFrozenPose>(holder);
                    em.AddBuffer<ConveyorLivePose>(holder);
                    em.AddComponentData(holder, new ConveyorPreviewRuntime());

                    st.PreviewEntity = holder;
                    st.HasStart = false;
                    st.StartConnector = Entity.Null;
                    st.SegmentsLocked = 0;
                    em.SetComponentData(gs, st);
                }
            }
            else
            {
                if (em.HasComponent<ConveyorState>(gs))
                {
                    var st = em.GetComponentData<ConveyorState>(gs);
                    if (st.PreviewEntity != Entity.Null && em.Exists(st.PreviewEntity))
                    {
                        if (em.HasBuffer<ConveyorGhostFrozenRef>(st.PreviewEntity))
                        {
                            var poolF = em.GetBuffer<ConveyorGhostFrozenRef>(st.PreviewEntity);
                            for (int i = 0; i < poolF.Length; i++)
                                if (em.Exists(poolF[i].Value)) em.DestroyEntity(poolF[i].Value);
                        }
                        if (em.HasBuffer<ConveyorGhostLiveRef>(st.PreviewEntity))
                        {
                            var poolL = em.GetBuffer<ConveyorGhostLiveRef>(st.PreviewEntity);
                            for (int i = 0; i < poolL.Length; i++)
                                if (em.Exists(poolL[i].Value)) em.DestroyEntity(poolL[i].Value);
                        }
                        em.DestroyEntity(st.PreviewEntity);
                    }
                    em.RemoveComponent<ConveyorState>(gs);
                }
            }
        }
    }
}