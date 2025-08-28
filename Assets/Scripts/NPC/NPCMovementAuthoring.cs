using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// MonoBehaviour для настройки параметров движения NPC в редакторе Unity.
/// Преобразуется в ECS-компоненты через Baker для использования в системах физики.
/// </summary>
[DisallowMultipleComponent]
public class NPCMovementAuthoring : MonoBehaviour
{
    [Header("Movement Settings")]
    
    /// <summary>
    /// Скорость передвижения NPC в метрах/секунду.
    /// </summary>
    [Tooltip("Скорость передвижения сущности в метрах/сек.")]
    public float Speed = 2.0f;
    
    /// <summary>
    /// Скорость поворота NPC в радианах/секунду.
    /// Определяет насколько быстро NPC может изменять направление движения.
    /// </summary>
    [Tooltip("Скорость поворота сущности")]
    public float RotationSpeed = 5.0f;
    
    /// <summary>
    /// Минимальное расстояние до цели, при котором NPC прекращает движение.
    /// </summary>
    [Tooltip("Расстояние до цели, на котором сущность прекратит движение.")]
    public float StoppingDistance = 0.5f;
    
    /// <summary>
    /// Порог скорости для обнуления движения.
    /// Если скорость ниже этого значения, считается как "остановленная".
    /// Хранится в квадрате для оптимизации вычислений.
    /// </summary>
    [Tooltip("Порог обнуления скорости движения")]
    public float VelocityZeroingThreshold = 0.001f;

    /// <summary>
    /// Baker-класс для преобразования данных из Authoring-компонента в ECS-компоненты.
    /// Регистрирует параметры движения в ECS-архитектуре для использования системой физики.
    /// </summary>
    public class Baker : Baker<NPCMovementAuthoring>
    {
        /// <summary>
        /// Преобразует данные из Authoring-компонента в ECS-компоненты.
        /// Создает и добавляет компоненты движения и базовой статистики.
        /// </summary>
        /// <param name="authoring">Исходный Authoring-компонент с настройками</param>
        public override void Bake(NPCMovementAuthoring authoring)
        {
            // Получаем сущность с динамическим использованием трансформа
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            
            // Добавляем основной компонент параметров движения
            AddComponent(entity, new NPCMovementComponent
            {
                // Базовые параметры из настроек
                Speed = authoring.Speed,
                RotationSpeed = authoring.RotationSpeed,
                StoppingDistance = authoring.StoppingDistance,
                
                // Квадрат порога скорости для оптимизации сравнений
                VelocityZeroingThresholdSq = authoring.VelocityZeroingThreshold * authoring.VelocityZeroingThreshold,
                
                // Инициализация целевой позиции и флага цели
                TargetPosition = float3.zero,
                HasTarget = false
            });
            
            // Добавляем компонент с базовыми параметрами движения
            // Используется для хранения неизменных значений при динамических изменениях
            AddComponent(entity, new NPCBaseMovementStats
            {
                StoppingDistance = authoring.StoppingDistance
            });
        }
    }
}
