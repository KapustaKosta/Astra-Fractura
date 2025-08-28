using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Unity.Physics;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(FinalizeBuildingSystem))]
public partial class FinalizeFoundationSystem : SystemBase
{
    // Порог вертикальной подмагнитки при финализации, если снапшот не содержит TargetDeckY
    const float HEIGHT_MAGNET_EPS = 0.09f; // ~9 см

    protected override void OnUpdate()
    {
        var em = EntityManager;
        var ecb = SystemAPI
            .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

        // читаем тюнинг (или дефолт)
        float placeMatchMaxDist = 6.0f;
        if (SystemAPI.TryGetSingleton<FoundationPlacementTuning>(out var tuning))
            placeMatchMaxDist = math.max(0f, tuning.PlaceMatchMaxDist);

        FoundationPlacementSnapshot snap = default;
        bool haveSnap = SystemAPI.TryGetSingleton(out snap) && snap.HasData != 0;

        // Путь 1: применяем снапшот к ближайшему "сырому" фундаменту (без Deck) 
        if (haveSnap)
        {
            Entity best = Entity.Null;
            float bestD2 = float.MaxValue;
            LocalTransform bestLT = default;
            FoundationTileHeight bestTileH = default;
            BuildingPivotOffset bestPivot = default;
            BuildingFootprint bestFp = default;
            PostTransformMatrix bestPost = default;
            bool hasPost = false;

            foreach (var (lt, tileH, entity) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<FoundationTileHeight>>()
                                                         .WithAll<FoundationTag>()
                                                         .WithNone<BuildingPreviewTag, FoundationDeck>()
                                                         .WithEntityAccess())
            {
                float2 dxz = new float2(lt.ValueRO.Position.x - snap.ExpectedPos.x,
                                        lt.ValueRO.Position.z - snap.ExpectedPos.z);
                float d2 = math.lengthsq(dxz);
                if (d2 < bestD2)
                {
                    bestD2 = d2;
                    best = entity;
                    bestLT = lt.ValueRO;
                    bestTileH = tileH.ValueRO;
                    if (em.HasComponent<BuildingPivotOffset>(entity)) bestPivot = em.GetComponentData<BuildingPivotOffset>(entity);
                    if (em.HasComponent<BuildingFootprint>(entity)) bestFp = em.GetComponentData<BuildingFootprint>(entity);
                    hasPost = em.HasComponent<PostTransformMatrix>(entity);
                    if (hasPost) bestPost = em.GetComponentData<PostTransformMatrix>(entity);
                }
            }

            if (best != Entity.Null && bestD2 <= placeMatchMaxDist * placeMatchMaxDist)
            {
                float baseH = math.max(0.01f, bestTileH.Value);
                float desiredScaleY;

                if (snap.HasTargetDeckY != 0)
                {
                    // Жёстко подгоняем DeckWorldY к целевому (идеальное совпадение по высоте с соседом)
                    float baseY = bestLT.Position.y + bestPivot.Value.y;
                    desiredScaleY = math.max(0.001f, (snap.TargetDeckY - baseY) / baseH);
                }
                else
                {
                    // Старое поведение: берём общую высоту с превью
                    desiredScaleY = math.max(0.001f, snap.TotalHeight / baseH);
                }

                ecb.SetComponent(best, new PostTransformMatrix { Value = float4x4.Scale(new float3(1f, desiredScaleY, 1f)) });
                ecb.AddComponent(best, new FoundationColliderScale { Y = desiredScaleY });

                float baseY2 = bestLT.Position.y + bestPivot.Value.y;
                float deckY = baseY2 + baseH * desiredScaleY;

                ecb.AddComponent(best, new FoundationDeck
                {
                    DeckWorldY = deckY,
                    CenterXZ = new float2(bestLT.Position.x, bestLT.Position.z),
                    SizeXZ = bestFp.Size,
                    Orientation = bestLT.Rotation
                });

                var clear = snap; clear.HasData = 0; clear.HasTargetDeckY = 0; clear.TargetDeckY = 0f; SystemAPI.SetSingleton(clear);
                return;
            }
        }

        // Путь 2: fallback — инициализируем все неопроцессенные фундаменты 
        foreach (var (lt, post, footprint, pivot, tileHeight, entity)
                 in SystemAPI.Query<RefRO<LocalTransform>, RefRW<PostTransformMatrix>, RefRO<BuildingFootprint>, RefRO<BuildingPivotOffset>, RefRO<FoundationTileHeight>>()
                              .WithAll<FoundationTag>()
                              .WithNone<BuildingPreviewTag, FoundationDeck>()
                              .WithEntityAccess())
        {
            float baseH = math.max(0.01f, tileHeight.ValueRO.Value);
            float desiredScaleY = 1f;

            float sYFromPost = math.abs(post.ValueRO.Value.c1.y);
            if (sYFromPost > 1e-4f)
                desiredScaleY = sYFromPost;
            else if (SystemAPI.TryGetSingleton<BuildingHeightOffset>(out var offs))
                desiredScaleY = math.max(0.1f, baseH + math.max(0f, offs.Value)) / baseH;

            // Предварительный scale
            float baseY = lt.ValueRO.Position.y + pivot.ValueRO.Value.y;
            float preliminaryDeckY = baseY + baseH * desiredScaleY;

            // Попробуем подмагнитить к ближайшей палубе по вертикали (если рядом)
            float2 myCenter = new float2(lt.ValueRO.Position.x, lt.ValueRO.Position.z);
            float bestD2 = float.MaxValue;
            float neighborDeckY = 0f;

            foreach (var deckRO in SystemAPI.Query<RefRO<FoundationDeck>>())
            {
                var deck = deckRO.ValueRO;
                float d2 = math.distancesq(myCenter, deck.CenterXZ);
                if (d2 < bestD2)
                {
                    bestD2 = d2;
                    neighborDeckY = deck.DeckWorldY;
                }
            }

            if (bestD2 <= placeMatchMaxDist * placeMatchMaxDist)
            {
                float diff = neighborDeckY - preliminaryDeckY;
                if (math.abs(diff) <= HEIGHT_MAGNET_EPS)
                {
                    desiredScaleY = math.max(0.001f, (neighborDeckY - baseY) / baseH);
                }
            }

            post.ValueRW.Value = float4x4.Scale(new float3(1f, desiredScaleY, 1f));
            ecb.AddComponent(entity, new FoundationColliderScale { Y = desiredScaleY });

            float finalDeckY = baseY + baseH * desiredScaleY;

            ecb.AddComponent(entity, new FoundationDeck
            {
                DeckWorldY = finalDeckY,
                CenterXZ = new float2(lt.ValueRO.Position.x, lt.ValueRO.Position.z),
                SizeXZ = footprint.ValueRO.Size,
                Orientation = lt.ValueRO.Rotation
            });
        }
    }
}