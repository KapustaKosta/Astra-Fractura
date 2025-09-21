using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("ECS/Building/Foundation Snap Visual Config")]
public class FoundationSnapVisualConfigAuthoring : MonoBehaviour
{
    public GameObject EdgeMarkerPrefab;
    public GameObject CornerMarkerPrefab;

    public float VisibleRange = 7f;
    public float BestScale = 1.15f;
    public float NormalScale = 1.0f;
    public float CornerScale = 1.0f;

    public Color BestColor = new Color(1f, 0.84f, 0.25f, 1f);
    public Color NormalColor = new Color(0.35f, 0.8f, 1f, 1f);

    class Baker : Unity.Entities.Baker<FoundationSnapVisualConfigAuthoring>
    {
        public override void Bake(FoundationSnapVisualConfigAuthoring a)
        {
            var e = GetEntity(TransformUsageFlags.None);
            var edge = a.EdgeMarkerPrefab ? GetEntity(a.EdgeMarkerPrefab, TransformUsageFlags.Renderable) : Entity.Null;
            var corner = a.CornerMarkerPrefab ? GetEntity(a.CornerMarkerPrefab, TransformUsageFlags.Renderable) : Entity.Null;

            AddComponent(e, new FoundationSnapVisualConfig
            {
                EdgeMarkerPrefab = edge,
                CornerMarkerPrefab = corner,
                VisibleRange = a.VisibleRange,
                BestScale = a.BestScale,
                NormalScale = a.NormalScale,
                CornerScale = a.CornerScale,
                BestColor = new float4(a.BestColor.linear.r, a.BestColor.linear.g, a.BestColor.linear.b, a.BestColor.linear.a),
                NormalColor = new float4(a.NormalColor.linear.r, a.NormalColor.linear.g, a.NormalColor.linear.b, a.NormalColor.linear.a)
            });
        }
    }
}

public struct FoundationSnapVisualConfig : IComponentData
{
    public Entity EdgeMarkerPrefab;
    public Entity CornerMarkerPrefab;
    public float VisibleRange;
    public float BestScale;
    public float NormalScale;
    public float CornerScale;
    public float4 BestColor;  
    public float4 NormalColor; 
}

public struct FoundationSnapMarkerTag : IComponentData
{
    public byte IsCorner;
}
