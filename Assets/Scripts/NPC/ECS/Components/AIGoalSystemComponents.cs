using System;
using Unity.Entities;


/// <summary>
/// Атрибут для GoalDefinition, который указывает, какие компоненты-теги
/// должны быть автоматически удалены с сущности NPC, когда эта цель
/// перестает быть активной. Это основной механизм очистки состояния.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class CleanupAttribute : Attribute
{
    /// <summary>
    /// Массив типов компонентов, подлежащих удалению.
    /// </summary>
    public readonly Type[] ComponentsToRemove;

    /// <summary>
    /// Конструктор атрибута очистки.
    /// </summary>
    /// <param name="componentsToRemove">Список типов компонентов для удаления.</param>
    public CleanupAttribute(params Type[] componentsToRemove)
    {
        ComponentsToRemove = componentsToRemove;
    }
}

/// <summary>
/// Компонент-запрос, который инициирует процесс очистки старой цели.
/// Добавляется к NPC в момент, когда ему назначается новая цель,
/// чтобы NPCTaskCleanupSystem мог удалить старые компоненты.
/// </summary>
public struct CleanupGoalRequest : IComponentData
{
    /// <summary>
    /// Тип старой цели, которая была заменена.
    /// Используется для поиска соответствующего CleanupAttribute.
    /// </summary>
    public GoalType OldGoalType;
}

/// <summary>
/// Компонент, хранящий ссылку на текущую активную цель для AI.
/// В отличие от ActiveGoal.Target, который может быть базой или другой общей целью,
/// этот компонент обычно указывает на конкретный объект в мире (например, конкретное дерево).
/// </summary>
public struct AIActiveTarget : IComponentData
{
    /// <summary>
    /// Сущность, являющаяся целью для текущего действия AI.
    /// </summary>
    public Entity Value;
}

/// <summary>
/// Компонент-таймер для отсчета времени выполнения иммерсивных действий (запуск, починка).
/// Добавляется к NPC, когда он "входит" в здание для работы.
/// </summary>
public struct MaintenanceTimer : IComponentData
{
    public float RemainingTime;
}

/// <summary>
/// Тег-состояние. Указывает, что назначенный цех NPC требует немедленного обслуживания.
/// Добавляется системой MaintenanceConditionSystem.
/// </summary>
public struct HasMaintenanceTaskTag : IComponentData { }