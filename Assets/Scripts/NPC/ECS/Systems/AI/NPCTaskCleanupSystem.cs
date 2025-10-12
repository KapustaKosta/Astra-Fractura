using System.Reflection;
using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Система очистки завершенных целей ИИ.
/// Удаляет компоненты, связанные с предыдущими целями NPC, перед началом новой задачи.
/// Обновляется в группе SimulationSystemGroup после NPCTaskArbiterSystem.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(NPCTaskArbiterSystem))] 
public partial class NPCTaskCleanupSystem : SystemBase
{
    // Ссылка на реестр целей ИИ
    private GoalRegistrySystem _goalRegistry;

    /// <summary>
    /// Инициализирует систему, получая доступ к реестру целей.
    /// Требует наличия GoalRegistrySystem.Initialized для работы.
    /// </summary>
    protected override void OnCreate()
    {
        base.OnCreate();
        // Получаем доступ к реестру целей
        _goalRegistry = World.GetOrCreateSystemManaged<GoalRegistrySystem>();
        // Требуем инициализации реестра
        RequireForUpdate<GoalRegistrySystem.Initialized>();
    }
    
    /// <summary>
    /// Основной метод системы, выполняющий очистку компонентов завершенных целей.
    /// Использует синхронный EntityCommandBuffer для немедленного выполнения изменений.
    /// </summary>
    protected override void OnUpdate()
    {
        // Создаем командный буфер для изменений
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        // Получаем доступ к реестру целей
        var goalDefinitions = _goalRegistry.GoalDefinitionsMap;

        // Обрабатываем запросы на очистку завершенных целей
        Entities
            .WithAll<CleanupGoalRequest>()
            .WithNone<HostileNPCTag>()
            .WithoutBurst() // Требуется для рефлексии и доступа к управляемым данным
            .ForEach((Entity entity, in CleanupGoalRequest request) =>
            {
                // Универсальная очистка: удаляем общие компоненты
                if (SystemAPI.HasComponent<MoveToRequest>(entity)) 
                    ecb.RemoveComponent<MoveToRequest>(entity);
                
                // Автоматическая очистка через атрибуты CleanupAttribute
                if (goalDefinitions.TryGetValue(request.OldGoalType, out var goalDef))
                {
                    // Получаем атрибут очистки для типа цели
                    var cleanupAttr = goalDef.GetType().GetCustomAttribute<CleanupAttribute>();
                    if (cleanupAttr != null)
                    {
                        // Удаляем указанные в атрибуте компоненты
                        foreach (var componentType in cleanupAttr.ComponentsToRemove)
                        {
                            // Проверяем наличие компонента перед удалением
                            if (EntityManager.HasComponent(entity, componentType))
                            {
                                ecb.RemoveComponent(entity, componentType);
                            }
                        }
                    }
                }
                
                // Удаляем сам запрос на очистку
                ecb.RemoveComponent<CleanupGoalRequest>(entity);

            }).Run();

        // Применяем изменения и очищаем память
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}