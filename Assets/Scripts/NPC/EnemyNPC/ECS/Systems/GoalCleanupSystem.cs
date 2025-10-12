using Unity.Entities;
using Unity.Transforms;

/// <summary>
/// Система, отвечающая за ПОЛНУЮ "очистку" состояния враждебного NPC
/// после отмены его задачи. Сбрасывает движение, поиск пути и боевые теги.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(CancelGoalOnTargetDeathSystem))]
[UpdateBefore(typeof(EnemyTaskArbiterSystem))]
public partial class EnemyGoalCleanupSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);

        // Запрос теперь ищет все компоненты, которые нужно сбросить.
        foreach (var (movement, pathfinding, pathBuffer, request, entity) in SystemAPI.Query<
                     RefRW<NPCMovementComponent>,
                     RefRW<NPCPathfindingComponent>,

                     DynamicBuffer<NPCPathBufferElement>,
                     RefRO<CleanupGoalRequest>>()
                     .WithAll<HostileNPCTag>()
                     .WithEntityAccess())
        {
            // 1. Очистка буфера пути 
            pathBuffer.Clear();

            // 2. Сброс компонента поиска пути 
            pathfinding.ValueRW.NeedsPathUpdate = false;
            pathfinding.ValueRW.CurrentWaypointIndex = 0;
            pathfinding.ValueRW.CurrentGoalTarget = Entity.Null;
            pathfinding.ValueRW.LastTargetPosition = float.PositiveInfinity; // Невалидная позиция

            // 3. Сброс компонента движения в состояние "покоя" 
            movement.ValueRW.HasTarget = false;
            movement.ValueRW.TargetPosition = SystemAPI.GetComponent<LocalTransform>(entity).Position;
            movement.ValueRW.CurrentDesiredMoveDirection = Unity.Mathematics.float3.zero;
            movement.ValueRW.PreferredVelocity = Unity.Mathematics.float3.zero;
            movement.ValueRW.TargetVelocity = Unity.Mathematics.float3.zero;

            // 4. Удаление тегов, связанных с предыдущей задачей 
            switch (request.ValueRO.OldGoalType)
            {
                case GoalType.Attack:
                    if (SystemAPI.HasComponent<IsAttackingTag>(entity))
                    {
                        ecb.RemoveComponent<IsAttackingTag>(entity);
                    }
                    break;
            }

            // 5. Удаляем сам компонент-запрос, чтобы он не обрабатывался снова 
            ecb.RemoveComponent<CleanupGoalRequest>(entity);
        }
    }
}