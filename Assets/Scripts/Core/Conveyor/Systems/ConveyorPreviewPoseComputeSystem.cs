using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Conveyor
{
    /// <summary>
    /// Первая фаза системы предпросмотра конвейера.
    /// Эта система читает ломаную линию, заданную пользователем (из буфера ConveyorPathPoint),
    /// вычисляет "приземленный" путь (аналогично финальной постройке) и рассчитывает
    /// позы (положение, вращение, длина) для "замороженных" и "живых" сегментов предпросмотра.
    /// Система не создает и не уничтожает сущности, а только заполняет буферы поз.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ConveyorPreviewLifecycleSystem))]
    [UpdateAfter(typeof(ConveyorPolylineInputSystem))]
    public partial class ConveyorPreviewPoseComputeSystem : SystemBase
    {
        // ID и сущность префаба кэшируются для оптимизации, чтобы не искать их каждый кадр.
        private int _cachedItemId = -1;
        private Entity _cachedPrefab = Entity.Null;

        /// <summary>
        /// Вызывается при создании системы. Устанавливает зависимости от синглтонов GameState и PhysicsWorldSingleton.
        /// </summary>
        protected override void OnCreate()
        {
            RequireForUpdate<GameState>();
            RequireForUpdate<PhysicsWorldSingleton>();
        }

        /// <summary>
        /// Выполняется каждый кадр. Основная логика по расчету поз для предпросмотра.
        /// </summary>
        protected override void OnUpdate()
        {
            var em = EntityManager;
            if (!SystemAPI.TryGetSingletonEntity<GameState>(out var gs)) return;
            if (!em.HasComponent<InConveyorMode>(gs)) return;
            if (!em.HasComponent<ConveyorState>(gs)) return;

            var st = em.GetComponentData<ConveyorState>(gs);
            var holder = st.PreviewEntity;
            if (holder == Entity.Null || !em.Exists(holder)) return;
            if (!em.HasBuffer<ConveyorPathPoint>(holder)) return;

            var path = em.GetBuffer<ConveyorPathPoint>(holder).ToNativeArray(Allocator.Temp);
            var frozenOut = em.GetBuffer<ConveyorFrozenPose>(holder);
            var liveOut   = em.GetBuffer<ConveyorLivePose>(holder);

            frozenOut.Clear();
            liveOut.Clear();

            if (path.Length < 2) { path.Dispose(); return; }

            // Получаем префаб и настройки сегмента, используя кэш
            if (_cachedItemId != st.ItemID || _cachedPrefab == Entity.Null || !em.Exists(_cachedPrefab))
            {
                _cachedPrefab = ItemToEntityResolver.GetEntityPrefabFromID(em, st.ItemID);
                _cachedItemId = st.ItemID;
            }
            var prefab = _cachedPrefab;

            // Получаем настройки длины из префаба или используем значения по умолчанию
            float baseLen = 6f, minLen = 1f, maxLen = 8.1f;
            if (prefab != Entity.Null && em.HasComponent<ConveyorSegmentSettings>(prefab))
            {
                var s = em.GetComponentData<ConveyorSegmentSettings>(prefab);
                if (s.Length     > 0) baseLen = s.Length;
                if (s.MinLength  > 0) minLen  = math.max(1f, s.MinLength);
                if (s.MaxLength  > 0) maxLen  = math.max(minLen, s.MaxLength);
            }
            minLen = math.clamp(minLen, 0.01f, maxLen - 1e-4f);

            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            const int GroundUnityLayer = 3;
            var groundOnlyFilter = new CollisionFilter
            {
                BelongsTo   = ~0u,
                CollidesWith= 1u << GroundUnityLayer,
                GroupIndex  = 0
            };

            // Шаг 1: Строим "приземленную" ломаную, находя высоту для каждой точки пути пользователя.
            var groundedPath = new NativeList<float3>(path.Length, Allocator.Temp);
            for (int i = 0; i < path.Length; i++)
            {
                groundedPath.Add(FindGroundHeight(path[i].Position, ref physicsWorld, groundOnlyFilter));
            }

            // Шаг 2: Разбиваем "приземленный" путь на сегменты стандартной длины, чтобы вычислить точки стыков (joints).
            // Это предотвращает наложение и обеспечивает плавные переходы высоты.
            var joints = new NativeList<float3>(Allocator.Temp);
            joints.Add(groundedPath[0]);

            for (int i = 0; i < groundedPath.Length - 1; i++)
            {
                float3 a = groundedPath[i];
                float3 b = groundedPath[i + 1];

                float3 dirXZ = b - a; dirXZ.y = 0;
                float lenXZ = math.length(new float2(dirXZ.x, dirXZ.z));
                if (lenXZ < 1e-4f) continue;

                ConveyorQuantization.QuantizeStraight(lenXZ, minLen, maxLen, out int cnt, out float per);

                for (int k = 0; k < cnt; k++)
                {
                    float t = (k + 1) / (float)cnt;
                    float3 splitXZ = math.lerp(a, b, t);
                    joints.Add(FindGroundHeight(splitXZ, ref physicsWorld, groundOnlyFilter));
                }
            }

            // Шаг 3: Формируем позы для "замороженных" и "живых" сегментов на основе точек стыков.
            int totalSegments = math.max(0, joints.Length - 1);
            int frozenSegments = math.max(0, totalSegments - 1);
            int liveSegments   = totalSegments - frozenSegments;

            // Заполняем буфер замороженных поз
            for (int i = 0; i < frozenSegments; i++)
            {
                float3 p0 = joints[i];
                float3 p1 = joints[i + 1];
                float3 vec = p1 - p0;
                float segLen = math.length(vec);
                if (segLen < 1e-4f) continue;
                
                var rot = quaternion.LookRotationSafe(math.normalizesafe(vec), math.up());

                frozenOut.Add(new ConveyorFrozenPose
                {
                    Position = p0 + vec * 0.5f,
                    Rotation = rot,
                    Length   = segLen
                });
            }
            
            // Заполняем буфер "живых" поз (последний сегмент, который тянется за курсором)
            if (liveSegments > 0)
            {
                float3 a = joints[joints.Length - 2];
                float3 b = joints[joints.Length - 1];
                float3 vec = b - a;
                float segLen = math.length(vec);

                if (segLen >= 1f)
                {
                    if (!em.HasComponent<ConveyorPreviewRuntime>(holder))
                        em.AddComponentData(holder, new ConveyorPreviewRuntime());
                    var runtime = em.GetComponentData<ConveyorPreviewRuntime>(holder);
                    
                    float3 vecXZ = vec; vecXZ.y = 0;
                    float lenXZ  = math.length(new float2(vecXZ.x, vecXZ.z));
                    ConveyorQuantization.QuantizeStraight(lenXZ, minLen, maxLen, out int rawCnt, out float perXZ);
                    
                    // Применяем гистерезис, чтобы избежать мерцания при изменении количества сегментов "хвоста"
                    int count = rawCnt;
                    float boundary = runtime.LastTailCount * maxLen;
                    float H = math.max(0.25f * maxLen, 0.5f);
                    if (runtime.LastTailCount > 0 && lenXZ > boundary - H && lenXZ < boundary + H)
                        count = runtime.LastTailCount;

                    float perXZAdj = math.clamp(lenXZ / math.max(count, 1), minLen, maxLen);
                    
                    for (int k = 0; k < count; k++)
                    {
                        float t0 = (k + 0) / (float)count;
                        float t1 = (k + 1) / (float)count;
                        
                        float3 b0XZ = math.lerp(a, b, t0);
                        float3 b1XZ = math.lerp(a, b, t1);
                        
                        float3 j0 = FindGroundHeight(b0XZ, ref physicsWorld, groundOnlyFilter);
                        float3 j1 = FindGroundHeight(b1XZ, ref physicsWorld, groundOnlyFilter);

                        float3 segV = j1 - j0;
                        float  L    = math.length(segV);
                        if (L < 1e-4f) continue;

                        var rot = quaternion.LookRotationSafe(math.normalizesafe(segV), math.up());

                        liveOut.Add(new ConveyorLivePose
                        {
                            Position = j0 + segV * 0.5f,
                            Rotation = rot,
                            Length   = L
                        });
                    }

                    runtime.LastTailCount  = count;
                    runtime.LastTailPerLen = perXZAdj; 
                    em.SetComponentData(holder, runtime);
                }
            }

            joints.Dispose();
            groundedPath.Dispose();
            path.Dispose();
        }
        
        /// <summary>
        /// Выполняет рейкаст вниз для определения высоты поверхности земли в заданной точке.
        /// </summary>
        /// <param name="position">Исходная позиция для поиска.</param>
        /// <param name="world">Физический мир для выполнения рейкаста.</param>
        /// <param name="filter">Фильтр коллайдеров, чтобы учитывать только землю.</param>
        /// <returns>Точка на поверхности земли. Если ничего не найдено, возвращает позицию с Y=0.</returns>
        private static float3 FindGroundHeight(float3 position, ref CollisionWorld world, CollisionFilter filter)
        {
            var rayInput = new RaycastInput
            {
                Start  = position + new float3(0, 100f, 0),
                End    = position - new float3(0, 200f, 0),
                Filter = filter
            };

            if (world.CastRay(rayInput, out var hit))
                return hit.Position;

            return new float3(position.x, 0, position.z);
        }
    }
}