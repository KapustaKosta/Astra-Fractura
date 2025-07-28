using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Система построения маршрутов для NPC на основе Unity NavMesh.
/// Обновляет путь для сущностей при необходимости, сохраняя результат в буфер путевых точек.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class NPCPathfindingSystem : SystemBase
{
    /// <summary>
    /// Радиус поиска ближайшей точки на навмеше относительно целевой позиции.
    /// Используется для коррекции целевой точки в пределах допустимой зоны.
    /// </summary>
    private const float NavMeshSampleRadius = 5.0f;

    /// <summary>
    /// Маска областей навмеша, используемых для построения маршрута.
    /// Включает все доступные области для максимальной гибкости.
    /// </summary>
    private const int NavMeshAreaMask = NavMesh.AllAreas;

    protected override void OnUpdate()
    {
        // Обрабатываем все сущности с компонентами навигации
        Entities
            .WithStructuralChanges() 
            .ForEach((Entity entity, ref NPCPathfindingComponent pathfinding, 
                     in LocalTransform transform, in NPCMovementComponent movement) =>
            {
                try
                {
                    // Пропускаем, если обновление маршрута не требуется
                    if (!pathfinding.NeedsPathUpdate)
                        return;

                    // Создаем экземпляр пути навмеша
                    var navMeshPath = new NavMeshPath();
                    
                    // Получаем начальную и целевую позиции
                    Vector3 start = new Vector3(transform.Position.x, transform.Position.y, transform.Position.z);
                    Vector3 end = new Vector3(movement.TargetPosition.x, movement.TargetPosition.y, movement.TargetPosition.z);

                    // Ищем ближайшую точку на навмеше к целевой позиции
                    NavMeshHit endHit;
                    bool endFound = NavMesh.SamplePosition(end, out endHit, NavMeshSampleRadius, NavMeshAreaMask);
                    
                    // Прерываем, если целевая точка недостижима
                    if (!endFound)
                    {
                        Debug.LogWarning($"[NPCPathfindingSystem] Entity {entity.Index}: Target position is unreachable on NavMesh.");
                        return;
                    }

                    // Строим путь от начальной до скорректированной целевой позиции
                    bool pathResult = NavMesh.CalculatePath(start, endHit.position, NavMeshAreaMask, navMeshPath);
                    
                    
                    Debug.Log($"[NPCPathfindingSystem] Entity {entity.Index}: CalculatePath result: {pathResult}");

                    if (pathResult)
                    {
                        
                        Debug.Log($"[NPCPathfindingSystem] Entity {entity.Index}: Path found, corners: {navMeshPath.corners.Length}");
                        
                        // Получаем или создаем буфер путевых точек
                        var buffer = EntityManager.HasBuffer<NPCPathBufferElement>(entity)
                            ? EntityManager.GetBuffer<NPCPathBufferElement>(entity)
                            : EntityManager.AddBuffer<NPCPathBufferElement>(entity);

                        // Очищаем и заполняем буфер новыми путевыми точками
                        buffer.Clear();
                        int i = 0;
                        foreach (var corner in navMeshPath.corners)
                        {
                            buffer.Add(new NPCPathBufferElement { Waypoint = new float3(corner.x, corner.y, corner.z) });
                            Debug.Log($"[NPCPathfindingSystem] Entity {entity.Index}: Waypoint {i}: {corner}");
                            i++;
                        }

                        // Обновляем параметры навигации
                        pathfinding.CurrentWaypointIndex = 0; // Сбрасываем на первую точку
                        pathfinding.LastTargetPosition = movement.TargetPosition; // Запоминаем целевую позицию
                        pathfinding.NeedsPathUpdate = false; // Сбрасываем флаг необходимости обновления
                    }
                    else
                    {
                        Debug.LogWarning($"[NPCPathfindingSystem] Entity {entity.Index}: Path not found!");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[NPCPathfindingSystem] Entity {entity.Index}: Exception: {ex}");
                }
            }).Run();
    }
}