using Unity.Entities;
using UnityEngine; 

/// <summary>
/// Система выполнения цели "Вернуться на базу" для ИИ.
/// Управляет перемещением NPC к поселению и инициирует разгрузку инвентаря.
/// Обновляется в группе SimulationSystemGroup после NPCTaskCleanupSystem.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(NPCTaskCleanupSystem))]
public partial class ReturnToBaseGoalExecutionSystem : SystemBase
{
    /// <summary>
    /// Основной метод системы, обрабатывающий выполнение цели возврата на базу.
    /// Управляет движением NPC к поселению и инициирует разгрузку при достижении цели.
    /// </summary>
    protected override void OnUpdate()
    {
        // Получаем командный буфер для изменения сущностей
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);
        // Получаем глобальные настройки AI
        var settings = SystemAPI.GetSingleton<AISettings>();
        
        // Обрабатываем всех ИИ-агентов с активной целью возврата к базе
        Entities
            .ForEach((Entity entity, in ActiveGoal goal, in NPCMovementComponent movement, 
                     in NPCPathfindingComponent pathfinding) =>
            {
                // Проверяем, что это цель на возврат на базу
                if (goal.Type != GoalType.ReturnToBase) return;
                
                // Определяем условия прибытия к базе:
                // 1. Движение завершено
                bool hasArrived = !movement.HasTarget;
                
                // 2. Цель завершенного движения совпадает с целевой базой
                bool arrivedAtCorrectPlace = pathfinding.CurrentGoalTarget == goal.Target;

                // Проверяем выполнение обоих условий прибытия
                if (hasArrived && arrivedAtCorrectPlace)
                {
                    // Проверяем, не был ли уже отправлен запрос на разгрузку
                    if (!SystemAPI.HasComponent<UnloadRequestTag>(entity))
                    {
                        #if UNITY_EDITOR
                        Debug.Log($"<color=green>[ReturnToBaseGoalSystem]</color> NPC {entity.Index} прибыл к правильной цели ({goal.Target.Index}). Добавление UnloadRequestTag.");
                        #endif
                        // Добавляем метку запроса разгрузки для инициации следующего этапа
                        ecb.AddComponent<UnloadRequestTag>(entity);
                    }
                }
            }).Run();
    }
}