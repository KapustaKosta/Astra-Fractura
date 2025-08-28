using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BuildingPlacementSystem))]
public partial struct FoundationSnapVisualSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BuildingPreviewTag>();
        state.RequireForUpdate<FoundationSnapVisualConfig>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        var config = SystemAPI.GetSingleton<FoundationSnapVisualConfig>();

        foreach (var (markerTag, entity) in SystemAPI.Query<FoundationSnapMarkerTag>().WithEntityAccess())
        {
            ecb.DestroyEntity(entity);
        }

        if (!SystemAPI.TryGetSingletonEntity<BuildingPreviewTag>(out var previewEntity) ||
            !SystemAPI.HasComponent<FoundationTag>(previewEntity))
        {
            return;
        }

        var previewTransform = SystemAPI.GetComponent<LocalTransform>(previewEntity);
        var snapPoints = new NativeList<SnapPointData>(Allocator.Temp);

        // ⬇️ ИЗМЕНЕНИЕ 3: Добавляем LocalToWorld в запрос, чтобы получить вращение фундамента.
        foreach (var (deck, ltw) in SystemAPI.Query<RefRO<FoundationDeck>, RefRO<LocalToWorld>>())
        {
            float2 deckHalfSize = deck.ValueRO.SizeXZ * 0.5f;
            float deckLocalY = 0; // В локальном пространстве палуба находится на Y, зависящем от скейла. Но DeckWorldY уже в мире, так что используем ее.

            // ⬇️ Определяем точки в ЛОКАЛЬНОМ пространстве фундамента (относительно его центра).
            // Y-координата пока 0, мы подставим мировую высоту DeckWorldY позже.
            float3[] localPoints =
            {
                // Углы
                new float3(deckHalfSize.x, 0, deckHalfSize.y),
                new float3(deckHalfSize.x, 0, -deckHalfSize.y),
                new float3(-deckHalfSize.x, 0, -deckHalfSize.y),
                new float3(-deckHalfSize.x, 0, deckHalfSize.y),
                // Ребра
                new float3(deckHalfSize.x, 0, 0),
                new float3(-deckHalfSize.x, 0, 0),
                new float3(0, 0, deckHalfSize.y),
                new float3(0, 0, -deckHalfSize.y)
            };

            for (int i = 0; i < localPoints.Length; i++)
            {
                // ⬇️ Трансформируем локальную точку в мировую, используя матрицу, которая содержит и позицию, и ВРАЩЕНИЕ.
                float3 worldPoint = math.transform(ltw.ValueRO.Value, localPoints[i]);
                // Y координата может быть неточной из-за скейла, поэтому переназначаем ее из надежного источника.
                worldPoint.y = deck.ValueRO.DeckWorldY;

                float distSq = math.distancesq(previewTransform.Position.xz, worldPoint.xz);
                if (distSq < config.VisibleRange * config.VisibleRange)
                {
                    snapPoints.Add(new SnapPointData
                    {
                        Position = worldPoint,
                        IsCorner = (byte)(i < 4 ? 1 : 0),
                        DistanceSq = distSq
                    });
                }
            }
        }

        if (snapPoints.Length == 0) return;

        SnapPointData bestSnapPoint = snapPoints[0];
        for (int i = 1; i < snapPoints.Length; i++)
        {
            if (snapPoints[i].DistanceSq < bestSnapPoint.DistanceSq)
            {
                bestSnapPoint = snapPoints[i];
            }
        }

        for (int i = 0; i < snapPoints.Length; i++)
        {
            var currentPoint = snapPoints[i];
            bool isBest = currentPoint.Equals(bestSnapPoint);

            Entity prefabToSpawn = currentPoint.IsCorner == 1 ? config.CornerMarkerPrefab : config.EdgeMarkerPrefab;
            if (prefabToSpawn == Entity.Null) continue;

            var newMarker = ecb.Instantiate(prefabToSpawn);
            float scale = isBest ? config.BestScale : (currentPoint.IsCorner == 1 ? config.CornerScale : config.NormalScale);
            float4 color = isBest ? config.BestColor : config.NormalColor;

            ecb.SetComponent(newMarker, LocalTransform.FromPositionRotationScale(currentPoint.Position, quaternion.identity, scale));
            ecb.AddComponent(newMarker, new URPMaterialPropertyBaseColor { Value = color });
            ecb.AddComponent(newMarker, new FoundationSnapMarkerTag { IsCorner = currentPoint.IsCorner });
        }

        snapPoints.Dispose();
    }

    private struct SnapPointData
    {
        public float3 Position;
        public float DistanceSq;
        public byte IsCorner;

        public bool Equals(SnapPointData other)
        {
            return Position.Equals(other.Position);
        }
    }
}