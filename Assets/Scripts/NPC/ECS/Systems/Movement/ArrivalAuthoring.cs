using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Компонент данных, хранящий параметры прибытия для целевой сущности.
/// Эти данные используются AI-системами для расчета точной точки назначения
/// и дистанции остановки NPC.
/// </summary>
public struct ArrivalData : IComponentData
{
    /// <summary>
    /// Смещение точки прибытия относительно центра цели в локальных координатах.
    /// Позволяет задать точку остановки не в центре, а, например, у двери здания.
    /// </summary>
    public float3 Offset;

    /// <summary>
    /// Радиус зоны остановки вокруг точки прибытия. NPC считается достигшим цели,
    /// когда он входит в эту зону.
    /// </summary>
    public float Radius;
}

/// <summary>
/// Authoring-компонент для настройки параметров прибытия в редакторе Unity.
/// "Запекается" в ECS-компонент ArrivalData.
/// </summary>
public class ArrivalAuthoring : MonoBehaviour
{
    [Header("Настройки прибытия")]
    [Tooltip("Смещение точки прибытия от центра объекта. Полезно для зданий, чтобы NPC подходил к двери.")]
    public float3 ArrivalOffset = float3.zero;

    [Tooltip("Радиус, на котором NPC должен остановиться от точки прибытия. Должен быть больше радиуса самого NPC.")]
    public float StoppingRadius = 1.0f;

    // Визуализируем зону прибытия в редакторе для удобства настройки
    private void OnDrawGizmosSelected()
    {
        // Рассчитываем мировую позицию точки прибытия
        Vector3 worldOffset = transform.rotation * ArrivalOffset;
        Vector3 arrivalPoint = transform.position + worldOffset;

        // Рисуем точку прибытия
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(arrivalPoint, 0.2f);

        // Рисуем радиус остановки
        Gizmos.color = new Color(0f, 1f, 1f, 0.25f);
        Gizmos.DrawSphere(arrivalPoint, StoppingRadius);
    }

    class Baker : Baker<ArrivalAuthoring>
    {
        public override void Bake(ArrivalAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new ArrivalData
            {
                Offset = authoring.ArrivalOffset,
                Radius = authoring.StoppingRadius
            });
        }
    }
}