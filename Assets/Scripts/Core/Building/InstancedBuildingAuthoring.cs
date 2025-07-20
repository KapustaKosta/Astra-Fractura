using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Authoring-компонент для настройки точки прибытия здания в редакторе Unity.
/// Позволяет задавать смещение от центра здания до точки, куда должны прибывать NPC.
/// Используется для гибкой настройки навигационных точек в сцене.
/// </summary>
public class InstancedBuildingAuthoring : MonoBehaviour
{
    /// <summary>
    /// Локальное смещение от центра (pivot) здания до точки прибытия NPC.
    /// Задает позицию в локальных координатах объекта, куда будут прибывать NPC.
    /// Пример: float3(0, 0, -2.5f) - точка на 2.5 метра перед зданием по оси Z.
    /// </summary>
    [Tooltip("Локальное смещение от центра здания до точки прибытия NPC")]
    public float3 ArrivalOffset = new float3(0, 0, -2.5f);
    
    class Baker : Baker<InstancedBuildingAuthoring>
    {
        public override void Bake(InstancedBuildingAuthoring authoring)
        {
            // Получаем сущность с динамическим использованием трансформа
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            
            // Добавляем компонент с данными о смещении точки прибытия
            AddComponent(entity, new ArrivalPointOffset
            {
                // Копируем смещение из настроек Authoring-компонента
                Value = authoring.ArrivalOffset
            });
        }
    }
}