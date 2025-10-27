using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

/// <summary>
/// Система, которая один раз при постройке находит для карьера
/// ближайший подходящий ресурсный узел в пределах радиуса взаимодействия.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class QuarryTargetingSystem : SystemBase
{
    private EntityQuery resourceNodeQuery;

    /// <summary>
    /// При создании системы кэширует запрос для поиска всех ресурсных узлов,
    /// чтобы повысить производительность и не создавать его в каждом кадре.
    /// </summary>
    protected override void OnCreate()
    {
        resourceNodeQuery = GetEntityQuery(typeof(ResourceNode), typeof(LocalToWorld));
    }

    /// <summary>
    /// Выполняется каждый кадр. Ищет все новопостроенные карьеры (`NewlyBuiltTag`),
    /// находит для каждого из них ближайший ресурсный узел и записывает его в состояние карьера (`QuarryState`).
    /// После обработки удаляет тег `NewlyBuiltTag`.
    /// </summary>
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

                    // Проверяем, что узел находится в пределах радиуса и ближе предыдущего найденного
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
        
        // Освобождаем память после завершения Job'а
        allResourceNodes.Dispose(Dependency);
    }
}