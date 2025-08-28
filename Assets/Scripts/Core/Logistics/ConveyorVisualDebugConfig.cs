using Unity.Entities;
using UnityEngine;

namespace Conveyor
{
    public struct ConveyorVisualDebugConfig : IComponentData
    {
        public bool Enable;
        public int MaxPerFrame;
    }

    public class ConveyorVisualDebugAuthoring : MonoBehaviour
    {
        public bool enable = true;
        public int maxPerFrame = 8;

        class Baker : Baker<ConveyorVisualDebugAuthoring>
        {
            public override void Bake(ConveyorVisualDebugAuthoring authoring)
            {
                var e = GetEntity(TransformUsageFlags.None);
                AddComponent(e, new ConveyorVisualDebugConfig
                {
                    Enable = authoring.enable,
                    MaxPerFrame = Mathf.Max(1, authoring.maxPerFrame)
                });
            }
        }
    }
}
