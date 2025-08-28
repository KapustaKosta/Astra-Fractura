using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(BuildingPlacementSystem))]
public partial class FoundationPreviewScalingSystem : SystemBase
{
    // Автокалибруемый "метров за один тик", если в тюнинге шаг не задан явно
    private float _observedStep = 0.10f;
    private float _lastOffset = 0f;

    protected override void OnUpdate()
    {
        // Без оффсета высоты от пользователя регулировать нечего
        if (!SystemAPI.TryGetSingleton<BuildingHeightOffset>(out var heightOffset))
            return;

        var em = EntityManager;
        var ecb = SystemAPI
            .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

        // читаем тюнинг (или дефолты)
        float heightSnapMaxDist = 6.0f;   // радиус по XZ для высотного снапа
        float sinkAmount = 0.10f;  // утопление (визуально/логически — но не влияет на масштаб)
        int snapTicks = 2;      // ±N тиков
        float stepMeters = 0f;     // 0 => авто
        float epsMeters = 0f;     // 0 => 25% от шага

        if (SystemAPI.TryGetSingleton<FoundationPlacementTuning>(out var t))
        {
            heightSnapMaxDist = math.max(0f, t.HeightSnapMaxDist);
            sinkAmount = math.max(0f, t.SinkAmount);
            snapTicks = math.max(1, t.HeightSnapTicks);
            stepMeters = math.max(0f, t.HeightScrollStep);
            epsMeters = math.max(0f, t.HeightSnapEpsilon);
        }

        // автокалибровка шага колеса (если явный шаг не задан)
        float deltaOff = math.abs(heightOffset.Value - _lastOffset);
        if (deltaOff > 1e-5f)
            _observedStep = math.lerp(_observedStep, deltaOff, 0.5f);
        _lastOffset = heightOffset.Value;

        float step = (stepMeters > 0f) ? stepMeters : math.max(0.01f, _observedStep);
        float eps = (epsMeters > 0f) ? epsMeters : math.max(0.005f, 0.25f * step);

        // Окно снапа в тиках (enter / exit с гистерезисом)
        float enterWindow = snapTicks * step + eps;
        float exitWindow = (snapTicks + 0.5f) * step + 2f * eps;

        // кэш всех палуб (целей для высотного снапа)
        var decks = new NativeList<FoundationDeck>(Allocator.Temp);
        foreach (var deck in SystemAPI.Query<RefRO<FoundationDeck>>())
            decks.Add(deck.ValueRO);

        // основной проход по превью фундаментов
        foreach (var (post, ltRO, tileHeight, entity)
                 in SystemAPI.Query<RefRW<PostTransformMatrix>, RefRO<LocalTransform>, RefRO<FoundationTileHeight>>()
                             .WithAll<BuildingPreviewTag, FoundationTag>()
                             .WithEntityAccess())
        {
            var lt = ltRO.ValueRO;

            float baseHeight = math.max(0.01f, tileHeight.ValueRO.Value);
            float pivotY = em.HasComponent<BuildingPivotOffset>(entity)
                                ? em.GetComponentData<BuildingPivotOffset>(entity).Value.y
                                : 0f;

            // Базовая высота по колесику: пользователь ВСЕГДА может регулировать её независимо от «взгляда»
            float userTotalHeight = math.max(0.1f, baseHeight + heightOffset.Value);
            float userScaleY = userTotalHeight / baseHeight;

            // Базовый уровень (нижняя грань фундамента): берем у сущности, без каких-либо поправок на groundY/sink
            // Важно: не используем PreviewGroundPosition для вычисления высоты — только для позиционирования в другой системе.
            float baseY = lt.Position.y + pivotY;

            // Для оценки XZ-близости к палубам используем дистанцию до периметра их OBB, а не до центра
            float2 posXZ = new float2(lt.Position.x, lt.Position.z);

            float bestEdgeDist = float.MaxValue;
            float bestDeckY = 0f;

            // Для OBB нужна "половинка" превью, чтобы правильно учитывать стык 
            float2 previewHalf = new float2(2f, 2f);
            if (em.HasComponent<BuildingFootprint>(entity))
            {
                var size = em.GetComponentData<BuildingFootprint>(entity).Size;
                previewHalf = size * 0.5f;
            }

            for (int i = 0; i < decks.Length; i++)
            {
                var d = decks[i];
                var rot = d.Orientation;
                var inv = math.inverse(rot);

                float2 deckHalf = d.SizeXZ * 0.5f;
                float3 deckCtr3 = new float3(d.CenterXZ.x, 0f, d.CenterXZ.y);
                float3 ptWorld = new float3(posXZ.x, 0f, posXZ.y);
                float3 ptLocal = math.mul(inv, (ptWorld - deckCtr3));

                float dx = math.max(math.abs(ptLocal.x) - (deckHalf.x + previewHalf.x), 0f);
                float dz = math.max(math.abs(ptLocal.z) - (deckHalf.y + previewHalf.y), 0f);
                float edgeDist = math.sqrt(dx * dx + dz * dz); // расстояние от точки до периметра прямоугольника

                if (edgeDist < bestEdgeDist)
                {
                    bestEdgeDist = edgeDist;
                    bestDeckY = d.DeckWorldY;
                }
            }

            // Было ли притяжение на предыдущем кадре
            bool hadSnap = em.HasComponent<PreviewHeightSnapState>(entity) &&
                           em.GetComponentData<PreviewHeightSnapState>(entity).IsActive != 0;

            // Текущий верх превью считаем чисто из baseY и userScaleY — полностью оторвано от ground/камеры
            float currentTop = baseY + baseHeight * userScaleY;
            float deltaToDeck = bestDeckY - currentTop;
            float absDelta = math.abs(deltaToDeck);

            bool nearByXZ = bestEdgeDist <= heightSnapMaxDist;
            bool enter = nearByXZ && !hadSnap && absDelta <= enterWindow;
            bool stay = nearByXZ && hadSnap && absDelta <= exitWindow;
            bool shouldSnap = enter || stay;

            if (shouldSnap)
            {
                // Жёстко подгоняем scaleY под DeckY, как в финализации:
                // deckY = baseY + baseHeight * scaleY  =>  scaleY = (deckY - baseY)/baseHeight
                float snappedScaleY = math.max(0.001f, (bestDeckY - baseY) / baseHeight);
                post.ValueRW.Value = float4x4.Scale(new float3(1f, snappedScaleY, 1f));

                var st = new PreviewHeightSnapState { TargetDeckY = bestDeckY, IsActive = 1 };
                if (em.HasComponent<PreviewHeightSnapState>(entity))
                    ecb.SetComponent(entity, st);
                else
                    ecb.AddComponent(entity, st);
            }
            else
            {
                // Свободный режим — только колесо управляет высотой (никаких поправок от ground/sink)
                post.ValueRW.Value = float4x4.Scale(new float3(1f, userScaleY, 1f));

                if (em.HasComponent<PreviewHeightSnapState>(entity))
                {
                    var st = em.GetComponentData<PreviewHeightSnapState>(entity);
                    if (st.IsActive != 0 || st.TargetDeckY != 0f)
                    {
                        st.IsActive = 0;
                        st.TargetDeckY = 0f;
                        ecb.SetComponent(entity, st);
                    }
                }
            }

        }

        decks.Dispose();
    }
}
