using Unity.Entities;
using UnityEngine;
using System;
using Unity.Mathematics; 
using Unity.Rendering;  

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

            AddComponent(entity, new ResourceNode
            {
                speedOfCollection = authoring.speedOfCollection,
                resourceType = authoring.resourceType,
                wealthDeposit = authoring.wealthDeposit
            });
            
            // Добавляем компонент для управления цветом, чтобы система подсветки могла работать.
            AddComponent(entity, new URPMaterialPropertyBaseColor { Value = new float4(1,1,1,1) });
        }
    }
}

public partial struct ResourceNode : IComponentData
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