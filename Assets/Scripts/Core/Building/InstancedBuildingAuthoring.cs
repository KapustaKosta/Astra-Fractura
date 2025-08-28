using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Authoring-компонент для настройки точки прибытия здания в редакторе Unity.
/// Позволяет задать смещение от pivot объекта до точки, куда будут прибывать NPC,
/// и визуализирует её гизмами прямо на префабе/в сцене.
/// </summary>
[ExecuteAlways]
public class InstancedBuildingAuthoring : MonoBehaviour
{

    /// <summary>
    /// Локальное смещение от центра (pivot) здания до точки прибытия NPC.
    /// Пример: float3(0, 0, 2.0f) — точка на 2м вперед по оси Z объекта.
    /// </summary>
    [Tooltip("Локальное смещение от pivot объекта до точки прибытия NPC (локальные координаты)")]
    public float3 ArrivalOffset = new float3(0, 0, 2.0f);


    [Header("Gizmos")]
    [Tooltip("Показывать гизмы всегда (в сцене и при выборе объекта)")]
    public bool AlwaysShowGizmos = true;

    [Tooltip("Цвет маркера точки прибытия")]
    public Color GizmoColor = new Color(0.2f, 0.8f, 1f, 1f);

    [Tooltip("Радиус сферического маркера точки прибытия (в метрах)")]
    [Min(0.01f)]
    public float GizmoRadius = 0.2f;

    [Tooltip("Показывать подпись у точки прибытия")]
    public bool ShowLabel = true;

    [Tooltip("Показывать стрелку направления вперед относительно объекта")]
    public bool ShowForwardArrow = true;

    [Tooltip("Длина стрелки направления (в метрах)")]
    [Min(0.1f)]
    public float ForwardArrowLength = 0.8f;

    [Tooltip("Показывать тонкую линию от pivot до точки")]
    public bool ShowLineFromPivot = true;


    private void OnDrawGizmos()
    {
        if (AlwaysShowGizmos)
            DrawArrivalGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (!AlwaysShowGizmos)
            DrawArrivalGizmos();
    }

    private void DrawArrivalGizmos()
    {
        // Мировая позиция точки прибытия
        Vector3 worldArrival = transform.TransformPoint(new Vector3(ArrivalOffset.x, ArrivalOffset.y, ArrivalOffset.z));

        // Сохраняем/восстанавливаем состояние гизмов
        Color prev = Gizmos.color;
        Matrix4x4 prevMtx = Gizmos.matrix;

        Gizmos.color = GizmoColor;
        Gizmos.matrix = Matrix4x4.identity;

        // Сфера-маркер
        Gizmos.DrawSphere(worldArrival, GizmoRadius);

        // Плоский диск для лучшей читаемости на земле
        Vector3 up = Vector3.up;
        Gizmos.DrawWireSphere(worldArrival, GizmoRadius * 1.5f);
#if UNITY_EDITOR
        // Тонкий диск на плоскости XZ:
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        Handles.color = new Color(GizmoColor.r, GizmoColor.g, GizmoColor.b, 0.25f);
        Handles.DrawWireDisc(worldArrival, up, GizmoRadius * 1.5f);
#endif

        // Линия от pivot к точке
        if (ShowLineFromPivot)
        {
            Gizmos.DrawLine(transform.position, worldArrival);
        }

#if UNITY_EDITOR
        // Подпись
        if (ShowLabel)
        {
            GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
            style.normal.textColor = GizmoColor;
            style.alignment = TextAnchor.MiddleLeft;

            // Немного сместим ярлык вверх
            Vector3 labelPos = worldArrival + Vector3.up * (GizmoRadius * 1.5f);
            Handles.Label(labelPos, "NPC Arrival", style);
        }

        // Стрелка направления вперёд относительно объекта
        if (ShowForwardArrow)
        {
            Vector3 forward = transform.forward;
            Vector3 arrowStart = worldArrival;
            Vector3 arrowEnd = worldArrival + forward * Mathf.Max(0.1f, ForwardArrowLength);

            Handles.color = GizmoColor;
            Handles.ArrowHandleCap(
                controlID: 0,
                position: arrowStart,
                rotation: Quaternion.LookRotation(forward, Vector3.up),
                size: ForwardArrowLength,
                eventType: EventType.Repaint
            );

            // Тонкая линия для тела стрелки (по желанию)
            Handles.DrawLine(arrowStart, arrowEnd);
        }
#endif

        // Восстановление состояния
        Gizmos.color = prev;
        Gizmos.matrix = prevMtx;
    }

    // BAKER

    class Baker : Baker<InstancedBuildingAuthoring>
    {
        public override void Bake(InstancedBuildingAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new ArrivalPointOffset
            {
                Value = authoring.ArrivalOffset
            });
        }
    }
}
