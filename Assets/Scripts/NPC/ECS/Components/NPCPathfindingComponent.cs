using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Experimental.AI;

/// <summary>
/// Компонент, управляющий состоянием навигации NPC.
/// Хранит данные о текущем маршруте, состоянии движения и целевой точке.
/// </summary>
public struct NPCPathfindingComponent : IComponentData
{
    /// <summary>
    /// Флаг необходимости обновления маршрута.
    /// Устанавливается, когда цель перемещается или требуется перестроение пути.
    /// </summary>
    public bool NeedsPathUpdate;
    
    /// <summary>
    /// Последняя зарегистрированная позиция цели.
    /// Используется для сравнения с текущей позицией цели при обновлении маршрута.
    /// </summary>
    public float3 LastTargetPosition;
    
    /// <summary>
    /// Индекс текущей путевой точки в маршруте.
    /// Определяет, к какой точке маршрута движется NPC на текущем этапе.
    /// </summary>
    public int CurrentWaypointIndex;
    
    /// <summary>
    /// Информация о расположении NPC на навмеше.
    /// Содержит данные о текущем регионе и координатах на навмеше.
    /// </summary>
    public NavMeshLocation NavMeshLocation;
    
    /// <summary>
    /// Целевая сущность, к которой движется NPC.
    /// Может быть врагом, ресурсом или другой точкой назначения.
    /// </summary>
    public Entity CurrentGoalTarget; 
}

/// <summary>
/// Элемент динамического буфера, представляющий отдельную путевую точку маршрута.
/// Используется для хранения последовательности точек для навигации NPC.
/// </summary>
public struct NPCPathBufferElement : IBufferElementData
{
    /// <summary>
    /// Координаты путевой точки в трехмерном пространстве.
    /// Определяет промежуточную точку маршрута, через которую должен пройти NPC.
    /// </summary>
    public float3 Waypoint;
}