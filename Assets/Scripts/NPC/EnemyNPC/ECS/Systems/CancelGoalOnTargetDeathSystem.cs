using Unity.Entities;

/// <summary>
/// Система, которая отменяет текущую задачу NPC, если его цель умерла.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(PlayerDeathSystem))]
[UpdateBefore(typeof(EnemyGoalCleanupSystem))] // Убедимся, что выполняемся до системы очистки
public partial class CancelGoalOnTargetDeathSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);

        // 'entity' в этом запросе - это и есть наш NPC.
        foreach (var (activeGoal, activeTarget, entity) in SystemAPI.Query<RefRO<ActiveGoal>, RefRO<AIActiveTarget>>().WithAll<HostileNPCTag>().WithEntityAccess())
        {
            Entity targetEntity = activeTarget.ValueRO.Value;

            if (SystemAPI.Exists(targetEntity) &&
                (SystemAPI.HasComponent<DeadTag>(targetEntity) || SystemAPI.HasComponent<IsDeadTag>(targetEntity)))
            {
                // Цель мертва. Удаляем компоненты цели и задачи.
                ecb.RemoveComponent<AIActiveTarget>(entity);
                ecb.RemoveComponent<ActiveGoal>(entity);
                
                // Добавляем компонент-запрос на очистку ПРЯМО К NPC.
                ecb.AddComponent(entity, new CleanupGoalRequest
                {
                    OldGoalType = activeGoal.ValueRO.Type
                });
            }
        }
    }
}