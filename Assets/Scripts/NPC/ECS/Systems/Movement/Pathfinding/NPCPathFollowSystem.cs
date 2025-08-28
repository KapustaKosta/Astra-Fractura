using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(NPCPathfindingSystem))]
public partial class NPCPathFollowSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities
            .ForEach((ref NPCMovementComponent movement,
                      ref NPCPathfindingComponent pathfinding,
                      in NPCBaseMovementStats baseStats,
                      in DynamicBuffer<NPCPathBufferElement> pathBuffer,
                      in LocalTransform transform) =>
            {
                if (!movement.HasTarget || pathBuffer.Length == 0)
                    return;

                int currentIndex = pathfinding.CurrentWaypointIndex;
                if (currentIndex >= pathBuffer.Length)
                    currentIndex = pathfinding.CurrentWaypointIndex = pathBuffer.Length - 1;

                float3 pos = transform.Position;
                float3 waypoint = pathBuffer[currentIndex].Waypoint;

                float dx = waypoint.x - pos.x;
                float dz = waypoint.z - pos.z;
                float distSq = dx * dx + dz * dz;


                bool isLast = currentIndex >= (pathBuffer.Length - 1);

                // Используем маленький порог для промежуточных точек, чтобы NPC проезжал их "по касательной"
                float waypointSwitchThreshold = math.clamp(baseStats.StoppingDistance * 0.5f, 0.15f, 0.25f);

                // Используем ПОЛНЫЙ радиус остановки для финальной точки
                float finalStopThreshold = math.max(baseStats.StoppingDistance, 0.1f);

                // Выбираем правильный порог в зависимости от того, последняя ли это точка
                float activeThreshold = isLast ? finalStopThreshold : waypointSwitchThreshold;

                // Проверяем, достигли ли мы текущей точки назначения
                if (distSq <= activeThreshold * activeThreshold)
                {
                    if (isLast)
                    {
                        // Если это была последняя точка, ЗАВЕРШАЕМ ДВИЖЕНИЕ.
                        // Это ключевое исправление.
                        movement.HasTarget = false;
                        Debug.Log($"[Follow] FINISHED path at idx={currentIndex}. pos={pos:F2}");
                        return;
                    }
                    else
                    {
                        // Если это промежуточная точка, переключаемся на следующую.
                        int next = currentIndex + 1;
                        pathfinding.CurrentWaypointIndex = next;
                        waypoint = pathBuffer[next].Waypoint;
                        // Проверяем, станет ли следующая точка последней
                        isLast = next >= (pathBuffer.Length - 1);
                        Debug.Log($"[Follow] -> next wp idx={next}/{pathBuffer.Length - 1}, pos={pos:F2}, nextWP={waypoint:F2}");
                    }
                }

                // Устанавливаем корректную текущую цель и порог для системы движения
                movement.TargetPosition = waypoint;
                // Для промежуточных точек ставим малый радиус, для последней - большой.
                movement.StoppingDistance = isLast ? finalStopThreshold : waypointSwitchThreshold;



                // Визуализация текущего отрезка
                Debug.DrawLine(pos, waypoint, isLast ? Color.green : Color.blue, 0.1f);
                // Debug.Log($"[Follow] idx={pathfinding.CurrentWaypointIndex}/{pathBuffer.Length - 1}, isLast={isLast}, dist={math.sqrt(distSq):F2}, stop={movement.StoppingDistance:F2}");

            })
            .Schedule();
    }
}