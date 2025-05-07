using Unity.Entities;
using UnityEngine;

public class BuildingAuthoringBaker : Baker<BuildingAuthoring>
{
    public override void Bake(BuildingAuthoring authoring)
    {
        if (authoring.buildingPrefab == null)
            return;

        Entity prefabEntity = GetEntity(authoring.buildingPrefab, TransformUsageFlags.Dynamic);

        var registryEntity = CreateAdditionalEntity(TransformUsageFlags.None);
        AddComponent(registryEntity, new BuildingPrefabReference
        {
            EntityPrefab = prefabEntity,
            ItemID = authoring.item.itemID
        });
    }
}

public struct BuildingPrefabReference : IComponentData
{
    public Entity EntityPrefab;
    public int ItemID;
}
