using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("ECS/Building/Foundation Authoring")]
public class FoundationAuthoring : MonoBehaviour
{
    [Header("Grid (м) — шаг снапа фундамента")]
    public Vector2 GridSize = new Vector2(4f, 4f);

    [Header("Footprint (м) — габариты плитки по XZ")]
    public Vector2 FootprintSize = new Vector2(4f, 4f);

    [Header("Смещение пивота по Y (если пивот не у подошвы)")]
    public float PivotYOffset = 0f;

    [Header("Базовая высота плитки (визуал)")]
    public float TileHeightMeters = 1f;

    class Baker : Unity.Entities.Baker<FoundationAuthoring>
    {
        public override void Bake(FoundationAuthoring a)
        {
            // primary entity из префаба/сцены
            var e = GetEntity(TransformUsageFlags.Dynamic);

            // Пишем ECS-компоненты из инспектора
            AddComponent(e, new FoundationTag
            {
                GridSize = new float2(a.GridSize.x, a.GridSize.y)
            });

            AddComponent(e, new BuildingFootprint
            {
                Size = new float2(a.FootprintSize.x, a.FootprintSize.y)
            });

            AddComponent(e, new BuildingPivotOffset
            {
                Value = new float3(0f, a.PivotYOffset, 0f)
            });

            AddComponent(e, new FoundationTileHeight
            {
                Value = a.TileHeightMeters
            });

            // Для неравномерного скейла по Y — всегда добавляем PostTransformMatrix
            // (LocalTransform поддерживает только uniform scale)
            AddComponent(e, new PostTransformMatrix { Value = float4x4.identity });
        }
    }
}
