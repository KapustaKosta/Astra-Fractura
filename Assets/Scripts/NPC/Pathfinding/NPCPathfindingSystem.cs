using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Система построения пути для NPC на основе Unity NavMesh.
/// Обновляет путь для сущностей с компонентом NPCPathfindingComponent,
/// если требуется пересчёт пути (NeedsPathUpdate = true).
/// Путь строится от текущей позиции до целевой позиции (TargetPosition) с учётом NavMesh.
/// Результат записывается в буфер NPCPathBufferElement.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class NPCPathfindingSystem : SystemBase
{
    /// <summary>
    /// Радиус поиска ближайшей точки на NavMesh к целевой позиции.
    /// </summary>
    private const float NavMeshSampleRadius = 5.0f;

    /// <summary>
    /// Маска областей NavMesh, используемая для поиска пути.
    /// </summary>
    private const int NavMeshAreaMask = NavMesh.AllAreas;

    protected override void OnUpdate()
    {
        Entities
            .WithStructuralChanges()
            .ForEach((Entity entity, ref NPCPathfindingComponent pathfinding, in LocalTransform transform, in NPCMovementComponent movement) =>
            {
                try
                {
                    if (!pathfinding.NeedsPathUpdate)
                        return;

                    var navMeshPath = new NavMeshPath();
                    Vector3 start = new Vector3(transform.Position.x, transform.Position.y, transform.Position.z);
                    Vector3 end = new Vector3(movement.TargetPosition.x, movement.TargetPosition.y, movement.TargetPosition.z);

                    // Поиск ближайшей точки на NavMesh к целевой позиции
                    NavMeshHit endHit;
                    bool endFound = NavMesh.SamplePosition(end, out endHit, NavMeshSampleRadius, NavMeshAreaMask);
                    if (!endFound)
                    {
                        Debug.LogWarning($"[NPCPathfindingSystem] Entity {entity.Index}: Target position is unreachable on NavMesh.");
                        return;
                    }

                    bool pathResult = NavMesh.CalculatePath(start, endHit.position, NavMeshAreaMask, navMeshPath);
                    Debug.Log($"[NPCPathfindingSystem] Entity {entity.Index}: CalculatePath result: {pathResult}");

                    if (pathResult)
                    {
                        Debug.Log($"[NPCPathfindingSystem] Entity {entity.Index}: Path found, corners: {navMeshPath.corners.Length}");
                        var buffer = EntityManager.HasBuffer<NPCPathBufferElement>(entity)
                            ? EntityManager.GetBuffer<NPCPathBufferElement>(entity)
                            : EntityManager.AddBuffer<NPCPathBufferElement>(entity);

                        buffer.Clear();
                        int i = 0;
                        foreach (var corner in navMeshPath.corners)
                        {
                            buffer.Add(new NPCPathBufferElement { Waypoint = new float3(corner.x, corner.y, corner.z) });
                            Debug.Log($"[NPCPathfindingSystem] Entity {entity.Index}: Waypoint {i}: {corner}");
                            i++;
                        }
                        pathfinding.CurrentWaypointIndex = 0;
                        pathfinding.LastTargetPosition = movement.TargetPosition;
                        pathfinding.NeedsPathUpdate = false;
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