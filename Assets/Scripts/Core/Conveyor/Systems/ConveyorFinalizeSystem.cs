using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using UnityEngine;

namespace Conveyor
{
    /// <summary>
    /// Система, отвечающая за финальное создание и размещение сегментов конвейера в мире
    /// после того, как игрок подтвердил его постройку. Она обрабатывает запрос на строительство,
    /// создает сущности сегментов, размещает их вдоль пути и устанавливает опоры.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ConfirmConveyorPlacementSystem))]
    public partial class ConveyorFinalizeSystem : SystemBase
    {
        /// <summary>
        /// Вызывается при создании системы. Устанавливает, что для работы системы
        /// необходимы синглтоны GameState и PlayerTag.
        /// </summary>
        protected override void OnCreate()
        {
            RequireForUpdate<GameState>();
            RequireForUpdate<PlayerTag>();
        }

        /// <summary>
        /// Выполняется каждый кадр. Обрабатывает запросы на постройку конвейеров,
        /// создает и настраивает сущности сегментов.
        /// </summary>
        protected override void OnUpdate()
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                               .CreateCommandBuffer(World.Unmanaged);
            var em = EntityManager;
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            
            // Фильтр для рейкаста, чтобы он попадал только в объекты на слое "Ground"
            const int GroundUnityLayer = 3;
            var groundOnlyFilter = new CollisionFilter
            {
                BelongsTo = ~0u,
                CollidesWith = 1u << GroundUnityLayer,
                GroupIndex = 0
            };
            
            if (!SystemAPI.TryGetSingletonEntity<GameState>(out var gs)) return;
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();

            // Запрос на обработку всех сущностей с запросом на постройку конвейера
            foreach (var (req, path, entity) in SystemAPI
                         .Query<RefRO<ConveyorBuildFromPathRequest>, DynamicBuffer<ConveyorBuildPathPoint>>()
                         .WithEntityAccess())
            {
                var request = req.ValueRO;
                // Проверка валидности запроса
                if (request.PreviewHolder == Entity.Null || !em.Exists(request.PreviewHolder) || path.Length < 2)
                {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                var prefab = ItemToEntityResolver.GetEntityPrefabFromID(em, request.ItemID);
                if (prefab == Entity.Null) { ecb.DestroyEntity(entity); continue; }

                // Проверяем, нужен ли опорный фундамент для конвейера
                bool needsFoundation = SystemAPI.HasComponent<RequiresDynamicFoundation>(prefab);
                Entity pillarPrefab = needsFoundation
                    ? SystemAPI.GetComponent<RequiresDynamicFoundation>(prefab).FoundationPrefab
                    : Entity.Null;

                // Получаем настройки длины из префаба, либо используем значения по умолчанию
                float baseLen = 6f, minLen = 1f, maxLen = 8.1f;
                if (em.HasComponent<ConveyorSegmentSettings>(prefab))
                {
                    var s = em.GetComponentData<ConveyorSegmentSettings>(prefab);
                    baseLen = s.Length > 0 ? s.Length : baseLen;
                    minLen = s.MinLength > 0 ? math.max(1f, s.MinLength) : minLen;
                    maxLen = s.MaxLength > 0 ? math.max(minLen, s.MaxLength) : maxLen;
                }
                minLen = math.clamp(minLen, 0.01f, maxLen - 1e-4f);

                // Создаем запрос на обновление коннекторов после завершения строительства
                var postBuildReq = ecb.CreateEntity();
                ecb.AddComponent(postBuildReq, new PostBuildConnectorUpdateRequest
                {
                    StartConnector = request.StartConnector,
                    EndConnector = request.EndConnector
                });
                var newSegs = ecb.AddBuffer<NewlyBuiltConveyorSegmentRef>(postBuildReq);
                
                // "Приземляем" все точки пути, находя для каждой высоту на поверхности земли
                var groundedPath = new NativeList<float3>(path.Length, Allocator.Temp);
                for (int i = 0; i < path.Length; i++)
                {
                    groundedPath.Add(FindGroundHeight(path[i].Position, ref physicsWorld, groundOnlyFilter));
                }
                
                // Список для хранения позиций стыков между сегментами
                var jointPositions = new NativeList<float3>(Allocator.Temp);

                // Проходим по "приземленному" пути и разбиваем его на сегменты
                for (int i = 0; i < groundedPath.Length - 1; i++)
                {
                    float3 a = groundedPath[i];
                    float3 b = groundedPath[i + 1];

                    if (i == 0)
                    {
                        jointPositions.Add(a);
                    }

                    // Строим по XZ, длину считаем по проекции
                    float3 dir = b - a; dir.y = 0;
                    float lenXZ = math.length(new float2(dir.x, dir.z));
                    if (lenXZ < 1e-4f) continue;
                    
                    // Разбиваем прямой участок на оптимальное количество сегментов
                    ConveyorQuantization.QuantizeStraight(lenXZ, minLen, maxLen, out int cnt, out float per);
                    
                    for (int k = 0; k < cnt; k++)
                    {
                        float t = (k + 1) / (float)cnt;
                        float3 jointPointXZ = math.lerp(a, b, t);
                        jointPositions.Add(FindGroundHeight(jointPointXZ, ref physicsWorld, groundOnlyFilter));
                    }
                }
                
                // Создаем сущности сегментов между вычисленными точками стыков
                for (int i = 0; i < jointPositions.Length - 1; i++)
                {
                    var startJoint = jointPositions[i];
                    var endJoint = jointPositions[i + 1];

                    var segmentVector = endJoint - startJoint;
                    var segmentCenter = startJoint + segmentVector * 0.5f;
                    var segmentLength = math.length(segmentVector);
                    
                    if (segmentLength < 1e-4f) continue;

                    var rotation = quaternion.LookRotationSafe(math.normalizesafe(segmentVector), math.up());

                    // Создаем экземпляр сегмента из префаба
                    var inst = ecb.Instantiate(prefab);
                    ecb.SetComponent(inst, LocalTransform.FromPositionRotation(segmentCenter, rotation));
                    
                    // Масштабируем модель сегмента по длине
                    float zScale = baseLen > 1e-4f ? segmentLength / baseLen : 1f;
                    ecb.AddComponent(inst, new PostTransformMatrix
                    {
                        Value = float4x4.Scale(new float3(1, 1, math.max(1e-4f, zScale)))
                    });
                    ecb.AddComponent(inst, new ConveyorSegmentScale { Z = zScale });
                    
                    // Записываем фактическую длину сегмента для использования другими системами
                    ecb.AddComponent(inst, new ConveyorSegmentRuntimeLength { Value = segmentLength });
                    
                    // Если нужна опора, создаем и масштабируем ее
                    if (needsFoundation && pillarPrefab != Entity.Null)
                    {
                        float3 groundUnderCenter = FindGroundHeight(segmentCenter, ref physicsWorld, groundOnlyFilter);
                        float pillarHeight = segmentCenter.y - groundUnderCenter.y;
                        
                        if (pillarHeight > 0.1f)
                        {
                            var pillarEntity = ecb.Instantiate(pillarPrefab);
                            var pillarPosition = new float3(segmentCenter.x, groundUnderCenter.y, segmentCenter.z);

                            ecb.SetComponent(pillarEntity, LocalTransform.FromPosition(pillarPosition));
                            ecb.AddComponent(pillarEntity, new PostTransformMatrix
                            {
                                Value = float4x4.Scale(new float3(1f, pillarHeight, 1f))
                            });

                            ecb.AppendToBuffer(inst, new LinkedEntityGroup { Value = pillarEntity });
                        }
                    }
                    
                    newSegs.Add(new NewlyBuiltConveyorSegmentRef { Value = inst });
                }

                int total = newSegs.Length;
                groundedPath.Dispose();
                jointPositions.Dispose();
                
                // Если были созданы сегменты, создаем запрос на списание ресурсов из инвентаря игрока
                if (total > 0)
                {
                    var removeItemReq = ecb.CreateEntity();
                    ecb.AddComponent(removeItemReq, new RemoveItemRequest
                    {
                        TargetInventoryOwner = playerEntity,
                        ItemID = request.ItemID,
                        Amount = total
                    });
                }

                if (em.Exists(request.PreviewHolder) && em.HasBuffer<ConveyorPathPoint>(request.PreviewHolder))
                    em.GetBuffer<ConveyorPathPoint>(request.PreviewHolder).Clear();

                // Сбрасываем состояние строительства конвейера
                if (em.HasComponent<ConveyorState>(gs))
                {
                    var conveyorState = em.GetComponentData<ConveyorState>(gs);
                    conveyorState.HasStart = false;
                    conveyorState.StartConnector = Entity.Null;
                    conveyorState.SegmentsLocked = 0;
                    ecb.SetComponent(gs, conveyorState);
                }

                // Уничтожаем исходный запрос на постройку
                ecb.DestroyEntity(entity);
            }
        }
        
        /// <summary>
        /// Определяет высоту поверхности земли в заданной точке XZ-координат.
        /// </summary>
        /// <param name="position">Исходная позиция для поиска.</param>
        /// <param name="physicsWorld">Физический мир для выполнения рейкаста.</param>
        /// <param name="filter">Фильтр коллайдеров, чтобы учитывать только землю.</param>
        /// <returns>Точка на поверхности земли. Если ничего не найдено, возвращает позицию с Y=0.</returns>
        private float3 FindGroundHeight(float3 position, ref CollisionWorld physicsWorld, CollisionFilter filter)
        {
            var rayInput = new RaycastInput
            {
                Start = position + new float3(0, 100f, 0),
                End = position - new float3(0, 200f, 0),
                Filter = filter
            };

            if (physicsWorld.CastRay(rayInput, out var hit))
            {
                return hit.Position;
            }
            
            return new float3(position.x, 0, position.z);
        }
    }
}