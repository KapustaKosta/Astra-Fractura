using Unity.Entities;
using Unity.Collections;
using UnityEngine;

namespace Conveyor.Authoring
{
    public class BuildingNameAuthoring : MonoBehaviour
    {
        public string BuildingName = "Building";

        class Baker : Baker<BuildingNameAuthoring>
        {
            public override void Bake(BuildingNameAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new BuildingName
                {
                    Value = new FixedString64Bytes(authoring.BuildingName)
                });
            }
        }
    }
}