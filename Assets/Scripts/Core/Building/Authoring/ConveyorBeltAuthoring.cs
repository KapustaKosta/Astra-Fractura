using Unity.Entities;
using UnityEngine;


[DisallowMultipleComponent]
public class ConveyorBeltAuthoring : MonoBehaviour
{
    [Tooltip("Тип точки подключения (Input/Output/Intermediate)")]
    public EndpointType type = EndpointType.Output;
    [Tooltip("Родительский объект (конвеер)")]
    public GameObject parentObject;
}

public class ConveyorBeltAuthoringBaker : Baker<ConveyorBeltAuthoring>
{
    public override void Bake(ConveyorBeltAuthoring authoring)
    {
        var beltEntity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent<ConveyorBeltTag>(beltEntity);
    }
}
