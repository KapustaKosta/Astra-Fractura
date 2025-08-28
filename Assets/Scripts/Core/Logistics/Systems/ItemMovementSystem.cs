using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Conveyor
{
    /// <summary>
    /// Спавнит визуалы для логических посылок (ItemInTransit) и ведёт их жизненный цикл.
    /// Подробно логирует состояние до/после и раз в 1 сек — позицию первого визуала.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(RouteTransferSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))] // писать LocalTransform до TransformGroup
    public partial class ItemMovementSystem : SystemBase
    {
        const float kBeltFullHeight = 1.6216f;        // Полная высота модели конвейера
        const float kBeltClearance = 0.02f;           // Маленький зазор для избежания Z-fighting
        const float kItemWorldHeight = 0.6f;          // РЕАЛЬНАЯ высота куба в мире
        const float kItemHalfHeight = kItemWorldHeight / 2.0f; // Половина высоты куба, чтобы поднять его над лентой

        // Итоговое смещение от центра конвейера вверх:
        // (половина высоты конвейера) + (зазор) + (половина высоты куба)
        const float kBeltTopOffset = 0.5f * kBeltFullHeight + kBeltClearance + kItemHalfHeight;

        // Debug
        private Entity _debugTrackedVisual;
        private double _nextDebugLogTime;
        private static bool sHeaderLogged;

        protected override void OnCreate()
        {
            base.OnCreate();
            if (!sHeaderLogged)
            {
                sHeaderLogged = true;
                Debug.Log("<color=#66ccff>[ItemMovementSystem]</color> created. Spawns visuals + verbose diagnostics.");
            }
        }

        protected override void OnUpdate()
        {
            // один ECB и один writer на кадр 
            var endSimEcb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = endSimEcb.CreateCommandBuffer(World.Unmanaged);
            var writer = ecb.AsParallelWriter();

            // 0) Быстрая сводка по состоянию мира (до джоб)
            PrintWorldSnapshotPre();

            // 1) Реестр ItemID -> Prefab (из Baker’а ItemVisualRegistryAuthoring)
            var registryQuery = GetEntityQuery(ComponentType.ReadOnly<ItemVisualPrefabReference>());
            var registry = registryQuery.ToComponentDataArray<ItemVisualPrefabReference>(Allocator.Temp);

            var map = new NativeHashMap<int, Entity>(math.max(1, registry.Length), Allocator.TempJob);
            for (int i = 0; i < registry.Length; i++)
                map.TryAdd(registry[i].ItemID, registry[i].EntityPrefab);

            if (registry.Length == 0)
                Debug.LogWarning("<color=#66ccff>[ItemMovementSystem]</color> Registry is EMPTY: no ItemVisualPrefabReference baked.");
            else
                //Debug.Log($"<color=#66ccff>[ItemMovementSystem]</color> Registry entries: {registry.Length}");

                registry.Dispose(); // Temp

            // 2) Lookups для джоб
            var pathLookup = GetBufferLookup<RoutePathElement>(true);
            var ltwLookup = GetComponentLookup<LocalToWorld>(true);
            var activeRouteLkUp = GetComponentLookup<ActiveRouteTag>(true);
            var hasTransitLkUp = GetComponentLookup<ItemInTransit>(true);

            // 3) Спавн визуалов (там, где их ещё нет)
            var spawnJob = new SpawnVisualsJob
            {
                ECB = writer,
                PathLookup = pathLookup,
                LtwLookup = ltwLookup,
                ActiveRouteLookup = activeRouteLkUp,
                ItemIdToPrefabMap = map,
                DefaultSpeed = 1.5f,
            };
            var hSpawn = spawnJob.ScheduleParallel(Dependency);

            // 4) Чистка «осиротевших» визуалов
            var cleanupJob = new CleanupVisualsJob
            {
                ECB = writer,
                HasTransit = hasTransitLkUp
            };
            var hCleanup = cleanupJob.ScheduleParallel(hSpawn);

            // 5) Освободить map после выполнения джоб и подписать зависимости
            map.Dispose(hCleanup);
            Dependency = hCleanup;

            // 6) Для стабильного дебага — дочитаем данные после джоб
            Dependency.Complete();

            // 7) Сводка после спавна
            PrintWorldSnapshotPost();

            // Выберем первый визуал (если ещё не выбран)
            if (_debugTrackedVisual == Entity.Null)
            {
                var anyVisualQuery = GetEntityQuery(
                    ComponentType.ReadOnly<ItemVisualTag>(),
                    ComponentType.ReadOnly<LocalToWorld>(),
                    ComponentType.ReadOnly<ConveyorVisualProgress>());

                using var visuals = anyVisualQuery.ToEntityArray(Allocator.Temp);
                if (visuals.Length > 0)
                    _debugTrackedVisual = visuals[0];
            }

            // Раз в 1 сек — лог позиции/прогресса первого визуала
            var now = SystemAPI.Time.ElapsedTime;
            if (_debugTrackedVisual != Entity.Null && now >= _nextDebugLogTime)
            {
                _nextDebugLogTime = now + 1.0;

                if (EntityManager.Exists(_debugTrackedVisual) &&
                    EntityManager.HasComponent<LocalToWorld>(_debugTrackedVisual) &&
                    EntityManager.HasComponent<ConveyorVisualProgress>(_debugTrackedVisual))
                {
                    var ltw = EntityManager.GetComponentData<LocalToWorld>(_debugTrackedVisual);
                    var prg = EntityManager.GetComponentData<ConveyorVisualProgress>(_debugTrackedVisual);

                    Debug.Log(
                        $"<color=yellow>[Conveyor/DEBUG]</color> first visual pos={ltw.Position}  " +
                        $"dist={prg.Distance:F3}/{prg.TotalLength:F3}  speed={prg.Speed:F2}"
                    );
                }
                else
                {
                    Debug.Log("<color=yellow>[Conveyor/DEBUG]</color> first visual missing or lacks components.");
                    _debugTrackedVisual = Entity.Null; // сбросим — выберем нового в следующем апдейте
                }
            }
        }

        // JOB: спавн визуалов 
        [BurstCompile]
        [WithNone(typeof(HasVisualTag))]
        public partial struct SpawnVisualsJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ECB;

            [ReadOnly] public BufferLookup<RoutePathElement> PathLookup;
            [ReadOnly] public ComponentLookup<LocalToWorld> LtwLookup;
            [ReadOnly] public ComponentLookup<ActiveRouteTag> ActiveRouteLookup;

            [ReadOnly] public NativeHashMap<int, Entity> ItemIdToPrefabMap;

            public float DefaultSpeed; // оставляем, но НЕ используем для установки Speed здесь

            void Execute([ChunkIndexInQuery] int sortKey, Entity transitEntity, in ItemInTransit item)
            {
                if (item.RouteEntity == Entity.Null) return;
                if (!ActiveRouteLookup.HasComponent(item.RouteEntity)) return;
                if (!PathLookup.HasBuffer(item.RouteEntity)) return;

                var path = PathLookup[item.RouteEntity];
                if (path.Length == 0) return;

                if (!ItemIdToPrefabMap.TryGetValue(item.ItemID, out var prefab))
                {
                    var it = ItemIdToPrefabMap.GetEnumerator();
                    if (it.MoveNext()) prefab = it.Current.Value;
                    it.Dispose();
                    if (prefab == Entity.Null) return;
                }

                // Стартовая позиция: первый сегмент + направление к следующему
                bool haveStart = false;
                float3 spawnPos = default;

                if (LtwLookup.HasComponent(path[0].SegmentEntity))
                {
                    var curLtw = LtwLookup[path[0].SegmentEntity];
                    float3 dir = curLtw.Forward;

                    if (path.Length > 1 && LtwLookup.HasComponent(path[1].SegmentEntity))
                    {
                        var nextPos = LtwLookup[path[1].SegmentEntity].Position;
                        var curPos = curLtw.Position;
                        var v = nextPos - curPos;
                        if (math.lengthsq(v) > 1e-6f) dir = math.normalize(v);
                    }

                    // ширина/длина секции нам неизвестна тут — возьмём безопасное смещение ~половины средней длины
                    const float approxHalf = 8.108f * 0.5f;
                    spawnPos = curLtw.Position - dir * approxHalf + curLtw.Up * kBeltTopOffset;
                    haveStart = true;
                }

                if (!haveStart) return;

                var visual = ECB.Instantiate(sortKey, prefab);

                ECB.AddComponent(sortKey, visual, new VisualFor { LogicalEntity = transitEntity });
                ECB.AddComponent<HasVisualTag>(sortKey, transitEntity);

                ECB.AddComponent(sortKey, visual, new ConveyorVisualProgress
                {
                    Distance = 0f,
                    TotalLength = 0f,   // выставит Init
                    Speed = 0f,   // выставит Init (TotalLength / TravelDuration)
                    SegmentIndex = 0,
                    SegmentStartDist = 0f
                });

                ECB.SetComponent(sortKey, visual, LocalTransform.FromPosition(spawnPos));
                ECB.AddComponent<ConveyorVisualNeedsInitTag>(sortKey, visual);
            }
        }

        // JOB: очистка осиротевших визуалов 
        [BurstCompile]
        public partial struct CleanupVisualsJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ECB;
            [ReadOnly] public ComponentLookup<ItemInTransit> HasTransit;

            void Execute([ChunkIndexInQuery] int sortKey, Entity visualEntity, in VisualFor link)
            {
                bool alive = link.LogicalEntity != Entity.Null && HasTransit.HasComponent(link.LogicalEntity);
                if (!alive)
                    ECB.DestroyEntity(sortKey, visualEntity);
            }
        }

        // Диагностика до/после спавна 
        private void PrintWorldSnapshotPre()
        {
            var qTransit = GetEntityQuery(ComponentType.ReadOnly<ItemInTransit>());
            var qNoVisual = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<ItemInTransit>() },
                None = new[] { ComponentType.ReadOnly<HasVisualTag>() }
            });
            var qVisualAll = GetEntityQuery(ComponentType.ReadOnly<ItemVisualTag>());
            var qVisualInitAny = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] {
                    ComponentType.ReadOnly<ItemVisualTag>(),
                    ComponentType.ReadOnly<ConveyorVisualNeedsInitTag>() },
                Options = EntityQueryOptions.IncludeDisabledEntities
            });

            int nTransit = qTransit.CalculateEntityCount();
            int nNoVisual = qNoVisual.CalculateEntityCount();
            int nVisual = qVisualAll.CalculateEntityCount();
            int nInitTotal = qVisualInitAny.CalculateEntityCount();

            //Debug.Log($"<color=#66ccff>[ItemMovementSystem/Pre]</color> transit={nTransit} needSpawn={nNoVisual} visuals={nVisual} visualsWithInitTag(total, any state)={nInitTotal}");

            // Кандидаты на спавн
            using var list = qNoVisual.ToEntityArray(Unity.Collections.Allocator.Temp);
            var transitLookup = GetComponentLookup<ItemInTransit>(true);
            var activeRouteLkUp = GetComponentLookup<ActiveRouteTag>(true);
            var pathLookup = GetBufferLookup<RoutePathElement>(true);

            int budget = math.min(4, list.Length);
            for (int i = 0; i < budget; i++)
            {
                var e = list[i];
                var t = transitLookup[e];
                bool hasActive = (t.RouteEntity != Entity.Null) && activeRouteLkUp.HasComponent(t.RouteEntity);
                bool hasPath = (t.RouteEntity != Entity.Null) && pathLookup.HasBuffer(t.RouteEntity);
                int pathLen = hasPath ? pathLookup[t.RouteEntity].Length : -1;

                //Debug.Log($"<color=#66ccff>[ItemMovementSystem/Pre]</color> candidate {e} itemID={t.ItemID} route={t.RouteEntity} active={hasActive} pathBuf={hasPath} pathLen={pathLen}");
            }
        }

        private void PrintWorldSnapshotPost()
        {
            var qVisualAll = GetEntityQuery(ComponentType.ReadOnly<ItemVisualTag>());
            var qInitAny = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] {
                    ComponentType.ReadOnly<ItemVisualTag>(),
                    ComponentType.ReadOnly<ConveyorVisualNeedsInitTag>() },
                Options = EntityQueryOptions.IncludeDisabledEntities
            });
            var qProgress = GetEntityQuery(ComponentType.ReadOnly<ConveyorVisualProgress>());

            //Debug.Log($"<color=#66ccff>[ItemMovementSystem/Post]</color> visuals={qVisualAll.CalculateEntityCount()} visuals(with InitTag any-state)={qInitAny.CalculateEntityCount()} withProgress={qProgress.CalculateEntityCount()}");
        }
    }
}
