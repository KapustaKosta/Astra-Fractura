using Unity.Entities;
using UnityEngine;

class ResourceNodeAuthoring : MonoBehaviour
{
    public int speedOfCollection;
    public ResourceCollectionType resourceType;
    public WealthDeposit wealthDeposit;

    class ResourceNodeAuthoringBaker : Baker<ResourceNodeAuthoring>
    {
        public override void Bake(ResourceNodeAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            //var resourceLayer = 1 << LayerMask.NameToLayer("Resources");
            //authoring.gameObject.layer = resourceLayer;

            AddComponent(entity, new ResourceNode
            {
                speedOfCollection = authoring.speedOfCollection,
                resourceType = authoring.resourceType,
                wealthDeposit = authoring.wealthDeposit
            });
        }
    }
}

public struct ResourceNode : IComponentData
{
    public int speedOfCollection;
    public ResourceCollectionType resourceType;
    public WealthDeposit wealthDeposit;
}

public enum ResourceCollectionType
{
    Wood,
    Stone,
    Food,
    Gold
}

public enum WealthDeposit
{
    Rich,
    Medium,
    Poor
}