using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AI;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class NPCPathfindingSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities
            .WithStructuralChanges()
            .ForEach((Entity entity, ref NPCPathfindingComponent pathfinding, in LocalTransform transform, in NPCMovementComponent movement) =>
            {
                try
                {
                    if (!pathfinding.NeedsPathUpdate)
                    {
                        //UnityEngine.Debug.Log($"[NPCPathfindingSystem] Entity {entity.Index}: Не требуется обновление пути");
                        return;
                    }

                    var navMeshPath = new NavMeshPath();
                    Vector3 start = new Vector3(transform.Position.x, transform.Position.y, transform.Position.z);
                    Vector3 end = new Vector3(movement.TargetPosition.x, movement.TargetPosition.y, movement.TargetPosition.z);

                    // Найти ближайшую точку на NavMesh к цели
                    NavMeshHit endHit;
                    bool endFound = NavMesh.SamplePosition(end, out endHit, 5.0f, NavMesh.AllAreas);
                    if (!endFound)
                    {
                        UnityEngine.Debug.LogWarning($"[NPCPathfindingSystem] Entity {entity.Index}: Target недостижим для агента с таким радиусом!");
                        return;
                    }

                    bool pathResult = NavMesh.CalculatePath(start, endHit.position, NavMesh.AllAreas, navMeshPath);
                    UnityEngine.Debug.Log($"[NPCPathfindingSystem] Entity {entity.Index}: CalculatePath result: {pathResult}");

                    if (pathResult)
                    {
                        UnityEngine.Debug.Log($"[NPCPathfindingSystem] Entity {entity.Index}: Путь построен, точек: {navMeshPath.corners.Length}");
                        var buffer = EntityManager.HasBuffer<NPCPathBufferElement>(entity)
                            ? EntityManager.GetBuffer<NPCPathBufferElement>(entity)
                            : EntityManager.AddBuffer<NPCPathBufferElement>(entity);

                        buffer.Clear();
                        int i = 0;
                        foreach (var corner in navMeshPath.corners)
                        {
                            buffer.Add(new NPCPathBufferElement { Waypoint = new float3(corner.x, corner.y, corner.z) });
                            UnityEngine.Debug.Log($"[NPCPathfindingSystem] Entity {entity.Index}: Waypoint {i}: {corner}");
                            i++;
                        }
                        pathfinding.CurrentWaypointIndex = 0;

                        pathfinding.LastTargetPosition = movement.TargetPosition;
                        pathfinding.NeedsPathUpdate = false;
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning($"[NPCPathfindingSystem] Entity {entity.Index}: Путь не найден!");
                    }
                }
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogError($"[NPCPathfindingSystem] Entity {entity.Index}: Exception: {ex}");
                }
            }).Run();
    }
}