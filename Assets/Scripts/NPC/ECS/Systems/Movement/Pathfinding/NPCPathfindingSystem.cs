using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Система, которая строит путь на NavMesh и записывает его в буфер NPCPathBufferElement.
/// Она не притягивает цель к ребру NavMesh; если цель находится вне сетки или за препятствием,
/// система выбирает ближайшую валидную точку и ищет полный маршрут вокруг препятствий.
/// Гарантирует, что агент никогда не будет направлен внутрь препятствия.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class NPCPathfindingSystem : SystemBase
{
    private const int   kAreaMask              = NavMesh.AllAreas;
    private const float kSampleRadiusStart     = 2.0f;
    private const float kSampleRadiusEnd       = 2.0f;
    private static readonly float[] kExtraRadii = { 3f, 5f, 8f, 12f, 16f };

    /// <summary>
    /// Основной метод обновления системы, выполняемый каждый кадр.
    /// Он находит все сущности, которым требуется обновление пути, и для каждой из них
    /// выполняет полный цикл поиска пути.
    /// </summary>
    protected override void OnUpdate()
    {
        Entities
            .WithStructuralChanges()
            .ForEach((Entity entity,
                      ref NPCPathfindingComponent pathfinding,
                      in LocalTransform transform,
                      in NPCMovementComponent movement) =>
            {
                /// <summary>
                /// Основная логика, выполняемая для каждой сущности, запросившей обновление пути.
                /// Проверяет валидность начальной и конечной точек на NavMesh, вызывает
                /// функцию для построения наилучшего возможного пути (полного или частичного)
                /// и записывает результат в буфер waypoints. В случае неудачи помечает
                /// сущность тегом MovementFailedTag.
                /// </summary>
                if (!pathfinding.NeedsPathUpdate)
                    return;

                pathfinding.NeedsPathUpdate = false;

                Vector3 desiredStart = transform.Position;
                Vector3 desiredEnd   = movement.TargetPosition;

                if (!NavMesh.SamplePosition(desiredStart, out var startHit, kSampleRadiusStart, kAreaMask))
                {
                    MarkMovementFailed(entity);
                    return;
                }

                if (!TrySampleEnd(desiredEnd, out var endHit, out _))
                {
                    MarkMovementFailed(entity);
                    return;
                }

                if (!TryBuildBestPath(startHit.position, endHit.position, out var bestPath, out _, out _))
                {
                    MarkMovementFailed(entity);
                    return;
                }

                var buffer = EntityManager.HasBuffer<NPCPathBufferElement>(entity)
                    ? EntityManager.GetBuffer<NPCPathBufferElement>(entity)
                    : EntityManager.AddBuffer<NPCPathBufferElement>(entity);

                buffer.Clear();
                WriteCornersToBuffer(bestPath, buffer);

                pathfinding.CurrentWaypointIndex = 0;
                pathfinding.LastTargetPosition   = movement.TargetPosition;

                if (EntityManager.HasComponent<MovementFailedTag>(entity))
                    EntityManager.RemoveComponent<MovementFailedTag>(entity);
            })
            .Run();
    }

    /// <summary>
    /// Пытается найти ближайшую валидную точку на NavMesh к заданной конечной позиции.
    /// Начинает с малого радиуса поиска и постепенно увеличивает его. В качестве крайней меры
    /// использует Raycast сверху, чтобы найти ближайшую границу NavMesh.
    /// </summary>
    /// <param name="desiredEnd">Желаемая конечная точка пути.</param>
    /// <param name="hit">Результат поиска, содержащий валидную позицию на NavMesh.</param>
    /// <param name="reason">Строка, описывающая, какой метод увенчался успехом.</param>
    /// <returns>True, если валидная точка была найдена, иначе false.</returns>
    private static bool TrySampleEnd(in Vector3 desiredEnd, out NavMeshHit hit, out string reason)
    {
        if (NavMesh.SamplePosition(desiredEnd, out hit, kSampleRadiusEnd, kAreaMask))
        {
            reason = "base";
            return true;
        }

        foreach (var r in kExtraRadii)
        {
            if (NavMesh.SamplePosition(desiredEnd, out hit, r, kAreaMask))
            {
                reason = $"radius+{r}";
                return true;
            }
        }

        var fromHigh = desiredEnd + Vector3.up * 5f;
        if (NavMesh.Raycast(fromHigh, desiredEnd, out var rayHit, kAreaMask))
        {
            var safe = rayHit.position + rayHit.normal * 0.20f;
            if (NavMesh.SamplePosition(safe, out hit, 1.0f, kAreaMask))
            {
                reason = "raycast-epsilon";
                return true;
            }
        }

        reason = "no-walkable-nearby";
        return false;
    }

    /// <summary>
    /// Пытается построить наилучший возможный путь от старта до цели.
    /// Сначала пытается найти прямой полный путь. Если это не удается, система ищет
    /// альтернативные точки назначения в кольцах вокруг исходной цели, чтобы найти полный
    /// путь в обход. Если и это не удается, возвращает лучший найденный частичный путь.
    /// </summary>
    /// <returns>True, если удалось найти хотя бы частичный путь, иначе false.</returns>
    private static bool TryBuildBestPath(Vector3 start, Vector3 end, out NavMeshPath best, out Vector3 bestEnd, out string why)
    {
        best    = new NavMeshPath();
        bestEnd = end;
        why     = "first";

        if (NavMesh.CalculatePath(start, end, kAreaMask, best) &&
            best.status == NavMeshPathStatus.PathComplete &&
            best.corners is { Length: > 0 })
        {
            return true;
        }

        var radii = new float[] { 1.0f, 2.0f, 3.0f };
        NavMeshPath bestPartial = null;
        Vector3     bestPartialEnd = end;
        float bestPartialEndDist = float.MaxValue;

        for (int i = 0; i < 16; i++)
        {
            float ang = (math.PI * 2f) * (i / 16f);
            var dir = new Vector3(math.cos(ang), 0, math.sin(ang));

            foreach (var r in radii)
            {
                var cand = end + dir * r;
                if (!NavMesh.SamplePosition(cand, out var candHit, 0.8f + r * 0.2f, kAreaMask))
                    continue;

                var p = new NavMeshPath();
                if (!NavMesh.CalculatePath(start, candHit.position, kAreaMask, p) || p.corners == null || p.corners.Length == 0)
                    continue;

                if (p.status == NavMeshPathStatus.PathComplete)
                {
                    best = p; bestEnd = candHit.position; why = $"ring-complete(r={r:0.0},i={i})";
                    return true;
                }
                
                float d = Vector3.SqrMagnitude(candHit.position - end);
                if (p.status == NavMeshPathStatus.PathPartial && d < bestPartialEndDist)
                {
                    bestPartial = p;
                    bestPartialEnd = candHit.position;
                    bestPartialEndDist = d;
                    why = $"ring-partial(r={r:0.0},i={i})";
                }
            }
        }

        if (bestPartial != null)
        {
            best    = bestPartial;
            bestEnd = bestPartialEnd;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Записывает угловые точки (waypoints) из объекта NavMeshPath в динамический буфер ECS.
    /// Фильтрует слишком близко расположенные точки и гарантирует, что в буфере будет минимум две точки.
    /// </summary>
    private static void WriteCornersToBuffer(NavMeshPath navPath, DynamicBuffer<NPCPathBufferElement> buffer)
    {
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
            buffer.Add(buffer[0]);
    }

    /// <summary>
    /// Добавляет компонент MovementFailedTag к сущности, сигнализируя другим системам о неудаче поиска пути.
    /// </summary>
    private void MarkMovementFailed(Entity e)
    {
        if (!EntityManager.HasComponent<MovementFailedTag>(e))
            EntityManager.AddComponent<MovementFailedTag>(e);
    }

    /// <summary>
    /// Вспомогательный метод для форматирования Vector3 в строку для логирования.
    /// </summary>
    private static string ToStr(in Vector3 v) => $"{v.x:0.##},{v.y:0.##},{v.z:0.##}";
}