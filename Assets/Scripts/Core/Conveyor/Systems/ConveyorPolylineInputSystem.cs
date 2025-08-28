using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Physics;
using Unity.Transforms;
using Unity.Physics.Systems;
using URay = UnityEngine.Ray;

namespace Conveyor
{
    /// <summary>
    /// Снимает курсор, примагничивает к земле и управляет точками ломаной.
    /// Работает ТОЛЬКО ПОСЛЕ выбора стартового коннектора.
    /// Не добавляет waypoint, если клик пришёлся по коннектору (даёт интеракции зафиналить).
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ConveyorPreviewLifecycleSystem))]     // превью точно есть
    [UpdateAfter(typeof(ConveyorPlacementInteractionSystem))] // после интеракции
    public partial class ConveyorPolylineInputSystem : SystemBase
    {
        private const float ScreenPxForConnector = 14f; // экранный порог «под курсором»
        private const float StrictPickMul = 0.75f;      // множитель к SnapRadius для строгого пика

        protected override void OnCreate()
        {
            RequireForUpdate<GameState>();
        }

        protected override void OnUpdate()
        {
            if (!SystemAPI.TryGetSingletonEntity<GameState>(out var gs)) return;
            if (!SystemAPI.HasComponent<InConveyorMode>(gs)) return;
            if (!SystemAPI.HasComponent<ConveyorState>(gs)) return;

            var st = SystemAPI.GetComponent<ConveyorState>(gs);


            if (!st.HasStart)
                return;

            var preview = st.PreviewEntity;
            if (preview == Entity.Null || !EntityManager.Exists(preview)) return;

        }

        private bool TryGetMouseOnGround(out float3 hitPos)
        {
            hitPos = default;
            var cam = Camera.main;
            if (cam == null) return false;

            URay ray = cam.ScreenPointToRay(Input.mousePosition);

            if (SystemAPI.HasSingleton<PhysicsWorldSingleton>())
            {
                var phys = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
                var input = new RaycastInput
                {
                    Start = ray.origin,
                    End = ray.origin + ray.direction * 10000f,
                    Filter = CollisionFilter.Default
                };

                if (phys.CastRay(input, out var hit))
                {
                    hitPos = hit.Position; // примагничивание к поверхности
                    return true;
                }
            }

            if (math.abs(ray.direction.y) < 1e-4f) return false;
            float t = -ray.origin.y / ray.direction.y;
            if (t < 0f) t = 0f;
            var q = ray.origin + ray.direction * t;
            hitPos = new float3(q.x, q.y, q.z);
            return true;
        }

        /// <summary>
        /// Возвращает true, если текущий клик ЛКМ пришёлся по ЛЮБОМУ коннектору.
        /// Использует строгий 3D-пик (ray→hit→радиус) + экранный порог, а также проверку подсвеченных.
        /// </summary>
        private bool IsClickOnAnyConnector(float snapRadius)
        {
            var cam = Camera.main;
            if (cam == null) return false;

            URay ray = cam.ScreenPointToRay(Input.mousePosition);
            float strictR = math.max(0.01f, snapRadius) * StrictPickMul;

            // 1) Строгий пик ближайшей к лучу точки мира
            if (TryPickConnectorStrict(ray, strictR, out var cand) && IsMouseOverEntityScreen(cand, ScreenPxForConnector))
                return true;

            // 2) Подсвеченные под курсором (как запасной вариант на сценах без коллайдеров)
            foreach (var (tag, ltw, e) in SystemAPI
                     .Query<RefRO<ConveyorConnectorHighlighted>, RefRO<LocalToWorld>>()
                     .WithEntityAccess())
            {
                if (IsMouseOverEntityScreen(e, ScreenPxForConnector))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Локальный строгий пик коннектора вокруг точки пересечения луча с миром.
        /// </summary>
        private bool TryPickConnectorStrict(URay ray, float radius, out Entity connector)
        {
            connector = Entity.Null;

            float3 worldHit;
            if (SystemAPI.HasSingleton<PhysicsWorldSingleton>())
            {
                var phys = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
                var input = new RaycastInput
                {
                    Start = ray.origin,
                    End = ray.origin + ray.direction * 10000f,
                    Filter = CollisionFilter.Default
                };

                if (phys.CastRay(input, out var hit))
                    worldHit = hit.Position;
                else
                {
                    // фолбэк: плоскость Y=0
                    if (math.abs(ray.direction.y) < 1e-6f) return false;
                    float t = -ray.origin.y / ray.direction.y;
                    worldHit = ray.origin + ray.direction * t;
                }
            }
            else
            {
                // нет физики: фолбэк на плоскость
                if (math.abs(ray.direction.y) < 1e-6f) return false;
                float t = -ray.origin.y / ray.direction.y;
                worldHit = ray.origin + ray.direction * t;
            }

            var em = EntityManager;
            var best = Entity.Null;
            var bestD = float.MaxValue;

            foreach (var (c, ltw, e) in SystemAPI
                     .Query<RefRO<ConveyorConnector>, RefRO<LocalToWorld>>()
                     .WithEntityAccess())
            {
                float d = math.distance(worldHit, ltw.ValueRO.Position);
                if (d <= radius && d < bestD)
                {
                    best = e;
                    bestD = d;
                }
            }

            if (best != Entity.Null) { connector = best; return true; }
            return false;
        }

        /// <summary>Курсор «над» сущностью (по экрану) в пределах пикселей.</summary>
        private bool IsMouseOverEntityScreen(Entity e, float maxPx)
        {
            var cam = Camera.main;
            if (cam == null) return false;
            if (!EntityManager.HasComponent<LocalToWorld>(e)) return false;

            var p = EntityManager.GetComponentData<LocalToWorld>(e).Position;
            var s = cam.WorldToScreenPoint(p);
            if (s.z < 0) return false;

            float dx = s.x - Input.mousePosition.x;
            float dy = s.y - Input.mousePosition.y;
            return math.length(new float2(dx, dy)) <= maxPx;
        }
    }
}