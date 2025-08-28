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
            .ForEach((Entity entity,
                      ref NPCPathfindingComponent pathfinding,
                      in LocalTransform transform,
                      in NPCMovementComponent movement) =>
            {
                if (!pathfinding.NeedsPathUpdate)
                    return;

                pathfinding.NeedsPathUpdate = false;

                Vector3 startPos = transform.Position;
                Vector3 endPos = movement.TargetPosition;

                if (!NavMesh.SamplePosition(startPos, out NavMeshHit startHit, NavMeshSampleRadius, NavMeshAreaMask))
                {
                    Debug.LogError($"[Pathfind] {entity.Index}: старт вне NM ({startPos:F2}).");
                    if (!EntityManager.HasComponent<MovementFailedTag>(entity))
                        EntityManager.AddComponent<MovementFailedTag>(entity);
                    return;
                }

                if (!NavMesh.SamplePosition(endPos, out NavMeshHit endHit, NavMeshSampleRadius, NavMeshAreaMask))
                {
                    Debug.LogWarning($"[Pathfind] {entity.Index}: цель вне NM ({endPos:F2}).");
                    if (!EntityManager.HasComponent<MovementFailedTag>(entity))
                        EntityManager.AddComponent<MovementFailedTag>(entity);
                    return;
                }

                // Доп. прижатие к краю
                {
                    Vector3 safeEnd = endHit.position;
                    if (NavMesh.FindClosestEdge(safeEnd, out NavMeshHit edgeHit, NavMeshAreaMask))
                        safeEnd = edgeHit.position;
                    endHit.position = safeEnd;
                }

                var navPath = new NavMeshPath();
                bool ok = NavMesh.CalculatePath(startHit.position, endHit.position, NavMeshAreaMask, navPath);

                if (!ok || navPath.status == NavMeshPathStatus.PathInvalid || navPath.corners == null || navPath.corners.Length == 0)
                {
                    Debug.LogWarning($"[Pathfind] {entity.Index}: путь не найден (status={navPath.status}).");
                    if (!EntityManager.HasComponent<MovementFailedTag>(entity))
                        EntityManager.AddComponent<MovementFailedTag>(entity);
                    return;
                }

                // Визуализация и логи статуса
                var color = navPath.status == NavMeshPathStatus.PathPartial ? Color.yellow : Color.cyan;
                for (int i = 0; i < navPath.corners.Length - 1; i++)
                    Debug.DrawLine(navPath.corners[i], navPath.corners[i + 1], color, 2f);

                Debug.Log($"[Pathfind] {entity.Index}: status={navPath.status}, corners={navPath.corners.Length}, start={startHit.position:F2}, end={endHit.position:F2}");

                // Заполнение буфера углов (с фильтрацией близких)
                var buffer = EntityManager.HasBuffer<NPCPathBufferElement>(entity)
                    ? EntityManager.GetBuffer<NPCPathBufferElement>(entity)
                    : EntityManager.AddBuffer<NPCPathBufferElement>(entity);

                buffer.Clear();

                const float minCornerDistSq = 0.01f;
                Vector3 prev = navPath.corners[0];
                buffer.Add(new NPCPathBufferElement { Waypoint = new float3(prev.x, prev.y, prev.z) });

                for (int i = 1; i < navPath.corners.Length; i++)
                {
                    Vector3 c = navPath.corners[i];
                    float dx = c.x - prev.x;
                    float dz = c.z - prev.z;
                    if (dx * dx + dz * dz >= minCornerDistSq)
                    {
                        buffer.Add(new NPCPathBufferElement { Waypoint = new float3(c.x, c.y, c.z) });
                        prev = c;
                    }
                }

                if (buffer.Length == 1)
                {
                    var last = buffer[0];
                    buffer.Add(last);
                }

                pathfinding.CurrentWaypointIndex = 0;
                pathfinding.LastTargetPosition = movement.TargetPosition;

                if (navPath.status == NavMeshPathStatus.PathPartial)
                    Debug.LogWarning($"[Pathfind] {entity.Index}: частичный маршрут — NPC остановится у края (ок).");

                if (EntityManager.HasComponent<MovementFailedTag>(entity))
                    EntityManager.RemoveComponent<MovementFailedTag>(entity);
            })
            .Run();
    }
}
