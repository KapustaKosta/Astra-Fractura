using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Experimental.AI;

//
// Компонент навигации NPC: хранит состояние маршрута, прогресс и метаданные.
// Без устаревших Experimental.AI типов.
//
public struct NPCPathfindingComponent : IComponentData
{
    /// Флаг необходимости перестроить путь (цель сместилась/застряли/и т.п.).
    public bool  NeedsPathUpdate;

    /// Последняя учтённая позиция цели (для детекта смещения цели).
    public float3 LastTargetPosition;

    /// Индекс текущей точки в буфере пути.
    public int    CurrentWaypointIndex;

    /// Текущая целевая сущность (игрок/ресурс/точка прибытия и т.д.).
    public Entity CurrentGoalTarget;

    /// Диагностика/ограничители на коррекции и избеганиях.
    public float  LastAvoidanceTime;
    public float  LastPathCorrectionTime;
}

/// Элемент буфера пути: одна путевая точка (мировые координаты).
public struct NPCPathBufferElement : IBufferElementData
{
    public float3 Waypoint;
}

/// Данные локального избегания (радиус, вес реакции и т.п.).
public struct AvoidanceData : IComponentData
{
    public float  Radius;
    public float  Weight;
    public float3 AvoidanceForce;
    public int    Priority;
}

/// Маркер: прибытие к цели завершено.
public struct ArrivedAtDestinationTag : IComponentData { }