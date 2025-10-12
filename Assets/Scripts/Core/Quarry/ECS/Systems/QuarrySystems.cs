using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Energy.Core; 

/// <summary>
/// Система, которая один раз при постройке находит для карьера
/// ближайший подходящий ресурсный узел.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class QuarryTargetingSystem : SystemBase
{
    private EntityQuery resourceNodeQuery;

    protected override void OnCreate()
    {
        // Создаем запрос один раз, чтобы не делать это в OnUpdate.
        resourceNodeQuery = GetEntityQuery(typeof(ResourceNode), typeof(LocalToWorld));
    }

    protected override void OnUpdate()
    {
        var resourceNodeTransformLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);
        resourceNodeTransformLookup.Update(this);
        
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged).AsParallelWriter();

        // Получаем все ресурсные узлы в виде массива для поиска.
        var allResourceNodes = resourceNodeQuery.ToEntityArray(Allocator.TempJob);

        // Ищем только новые карьеры, у которых еще нет цели.
        Entities
            .WithAll<QuarryTag, NewlyBuiltTag>()
            .WithReadOnly(resourceNodeTransformLookup)
            .WithReadOnly(allResourceNodes)
            .ForEach((Entity quarryEntity, int entityInQueryIndex, ref QuarryState state, in LocalToWorld quarryTransform, in QuarrySettings settings) =>
            {
                if (state.TargetResourceNode != Entity.Null) return;

                Entity closestNode = Entity.Null;
                float closestDistSq = float.MaxValue;

                // Проходим по всем ресурсным узлам в мире
                foreach (var nodeEntity in allResourceNodes)
                {
                    var nodeTransform = resourceNodeTransformLookup[nodeEntity];
                    float distSq = math.distancesq(quarryTransform.Position, nodeTransform.Position);

                    if (distSq <= settings.InteractionRange * settings.InteractionRange && distSq < closestDistSq)
                    {
                        closestDistSq = distSq;
                        closestNode = nodeEntity;
                    }
                }

                if (closestNode != Entity.Null)
                {
                    state.TargetResourceNode = closestNode;
                }

                // Убираем тег у конкретной сущности, используя entityInQueryIndex для параллельной записи.
                ecb.RemoveComponent<NewlyBuiltTag>(entityInQueryIndex, quarryEntity);

            }).ScheduleParallel(); 
        
        allResourceNodes.Dispose(Dependency);
    }
}

/// <summary>
/// Система-сенсор, которая проверяет, заполнен ли инвентарь карьера,
/// и добавляет/убирает тег QuarryInventoryFullTag.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(InventorySystem))]
public partial class QuarryInventoryStateSystem : SystemBase
{
    protected override void OnCreate()
    {
        // Система будет работать, только если в мире есть хотя бы один карьер.
        RequireForUpdate<QuarryTag>();
    }

    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        var itemRegistry = ItemRegistry.Instance;
        if (itemRegistry == null) return;

        // Этот запрос выполняется на главном потоке, поэтому он проще.
        foreach (var (inventory, entity) in SystemAPI.Query<DynamicBuffer<InventoryItemElement>>().WithAll<QuarryTag>().WithEntityAccess())
        {
            bool isCurrentlyFull = InventoryUtils.IsInventoryFull(inventory, itemRegistry);
            bool hasTag = SystemAPI.HasComponent<QuarryInventoryFullTag>(entity);

            if (isCurrentlyFull && !hasTag)
            {
                ecb.AddComponent<QuarryInventoryFullTag>(entity);
            }
            else if (!isCurrentlyFull && hasTag)
            {
                ecb.RemoveComponent<QuarryInventoryFullTag>(entity);
            }
        }
    }
}

/// <summary>
/// Основная система, управляющая логикой добычи карьера.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(QuarryTargetingSystem))]
public partial class QuarryHarvestingSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged).AsParallelWriter();

        float currentTime = (float)SystemAPI.Time.ElapsedTime;

        // Так же, как и в другой системе, обновляем lookup перед использованием в job.
        var resourceNodeLookup = SystemAPI.GetComponentLookup<ResourceNode>(true);
        resourceNodeLookup.Update(this);

        Entities
            .WithAll<QuarryTag>()
            .WithReadOnly(resourceNodeLookup)
            .ForEach((Entity entity, int entityInQueryIndex, ref QuarryState state, ref ConsumerLoad load, in QuarrySettings settings) =>
            {
                // 1. Определяем условия работы
                bool isInventoryFull = SystemAPI.HasComponent<QuarryInventoryFullTag>(entity);
                bool hasPower = SystemAPI.HasComponent<HasPowerTag>(entity);
                bool hasValidTarget = state.TargetResourceNode != Entity.Null && resourceNodeLookup.HasComponent(state.TargetResourceNode);

                bool canWork = hasPower && !isInventoryFull && hasValidTarget;

                // 2. Действуем на основе условий
                if (canWork)
                {
                    load.CurrentKW = settings.EnergyConsumptionKW;

                    if (currentTime >= state.LastHarvestTime + settings.HarvestInterval)
                    {
                        var requestEntity = ecb.CreateEntity(entityInQueryIndex);
                        ecb.AddComponent(entityInQueryIndex, requestEntity, new ValidateHarvestAttemptRequest
                        {
                            Harvester = entity,
                            TargetResourceNode = state.TargetResourceNode
                        });

                        state.LastHarvestTime = currentTime;
                    }
                }
                else
                {
                    load.CurrentKW = 0f;
                }
            }).ScheduleParallel();
    }
}