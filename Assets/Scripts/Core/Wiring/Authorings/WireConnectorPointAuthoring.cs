using UnityEngine;
using Unity.Entities;
using Unity.Rendering;      // DisableRendering, URPMaterialPropertyBaseColor
using Unity.Mathematics;
using Wiring;               // ConnectedWires

/// Авторинг на дочернем GO-коннекторе (у него есть MeshFilter+MeshRenderer)
[DisallowMultipleComponent]
public sealed class WireConnectorPointAuthoring : MonoBehaviour
{
    [Min(0f)] public float Radius = 0.1f;
    public int Index = 0;
}

/// Данные коннектора
public struct WireConnector : IComponentData
{
    public int Index;
    public float Radius;
}

public sealed class WireConnectorPointBaker : Baker<WireConnectorPointAuthoring>
{
    public override void Bake(WireConnectorPointAuthoring a)
    {
        var e = GetEntity(a, TransformUsageFlags.Renderable);

        AddComponent(e, new WireConnector { Index = a.Index, Radius = a.Radius });
        AddComponent(e, new URPMaterialPropertyBaseColor { Value = new float4(1, 1, 1, 1) });
        AddBuffer<ConnectedWires>(e);
        AddComponent<DisableRendering>(e);
        AddComponent<WireConnectorHighlightedTag>(e);
        SetComponentEnabled<WireConnectorHighlightedTag>(e, false);

    }
}