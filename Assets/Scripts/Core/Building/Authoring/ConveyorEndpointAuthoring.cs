using Unity.Entities;
using UnityEngine;


[DisallowMultipleComponent]
public class ConveyorEndpointAuthoring : MonoBehaviour
{
    [Tooltip("Тип точки подключения (Input/Output/Intermediate)")]
    public EndpointType type = EndpointType.Input;
    [Tooltip("Родительский объект (здание или конвеер)")]
    public GameObject parentObject;
}

public class ConveyorEndpointAuthoringBaker : Baker<ConveyorEndpointAuthoring>
{
    public override void Bake(ConveyorEndpointAuthoring authoring)
    {
        var endpointEntity = GetEntity(TransformUsageFlags.Dynamic);
        var parentEntity = authoring.parentObject != null
            ? GetEntity(authoring.parentObject, TransformUsageFlags.Dynamic)
            : Entity.Null;
        AddComponent(endpointEntity, new ConveyorEndpoint
        {
            ParentEntity = parentEntity,
            IsInput = authoring.type == EndpointType.Input,
            Type = authoring.type
        });
    }
}
