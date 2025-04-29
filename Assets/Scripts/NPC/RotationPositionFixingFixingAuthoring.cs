using Unity.Entities;
using UnityEngine;

class RotationPositionFixingAuthoring : MonoBehaviour
{
    class Baker : Baker<RotationPositionFixingAuthoring>
    {
        public override void Bake(RotationPositionFixingAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new RotationPositionFixing { isFixed = false });
        }
    }
}

public struct RotationPositionFixing : IComponentData
{
    public bool isFixed;
}
