using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(FoundationPlacementSystem))]
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
        
        foreach (var (deck, ltw) in SystemAPI.Query<RefRO<FoundationDeck>, RefRO<LocalToWorld>>())
        {
            float2 deckHalfSize = deck.ValueRO.SizeXZ * 0.5f;
            float deckLocalY = 0; // В локальном пространстве палуба находится на Y, зависящем от скейла. Но DeckWorldY уже в мире, так что используем ее.

            // Определяем точки в локальном пространстве фундамента (относительно его центра).
            // Y-координата пока 0, мы подставим мировую высоту DeckWorldY позже.
            // Используем NativeArray вместо управляемого массива для совместимости с Burst
            var localPoints = new NativeArray<float3>(8, Allocator.Temp);
            localPoints[0] = new float3(deckHalfSize.x, 0, deckHalfSize.y);
            localPoints[1] = new float3(deckHalfSize.x, 0, -deckHalfSize.y);
            localPoints[2] = new float3(-deckHalfSize.x, 0, -deckHalfSize.y);
            localPoints[3] = new float3(-deckHalfSize.x, 0, deckHalfSize.y);
            localPoints[4] = new float3(deckHalfSize.x, 0, 0);
            localPoints[5] = new float3(-deckHalfSize.x, 0, 0);
            localPoints[6] = new float3(0, 0, deckHalfSize.y);
            localPoints[7] = new float3(0, 0, -deckHalfSize.y);

            for (int i = 0; i < localPoints.Length; i++)
            {
                // рансформируем локальную точку в мировую, используя матрицу, которая содержит и позицию, и ВРАЩЕНИЕ.
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
            
            localPoints.Dispose(); // Не забываем освобождать память
        }

        if (snapPoints.Length == 0)
        {
            snapPoints.Dispose();
            return; // Выходим, если нет точек
        }

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