using Unity.Entities;
using UnityEngine;

/// <summary>
/// Authoring-компонент для "запекания" настроек ИИ в сущность-синглтон.
/// </summary>
public class AISettingsAuthoring : MonoBehaviour
{
    [Header("Общие настройки")]
    [Tooltip("Радиус, в котором NPC ищут ближайшие цели (ресурсы).")]
    public float AISearchRadius = 100f;

    [Tooltip("Индекс физического слоя, на котором находятся все ресурсы. Настраивается в Project Settings -> Physics.")]
    public int ResourceCollisionLayer = 8;
    
    [Header("Приоритеты задач от игрока")]
    [Tooltip("Оценка 'полезности', которая назначается задаче добычи, отданной игроком напрямую.")]
    public float PlayerAssignHarvestPriority = 999f;
    [Tooltip("Оценка 'полезности', которая назначается задаче возвращения на базу, когда инвентарь NPC полон.")]
    public float PlayerAssignReturnPriority = 1000f;

    [Header("Настройки движения")]
    [Tooltip("Скорость поворота NPC к цели.")]
    public float RotationSpeed = 10f;
    [Tooltip("Множитель для дистанции остановки при добыче (0.9 = остановиться на 90% от радиуса взаимодействия).")]
    [Range(0.1f, 1f)]
    public float HarvestInteractionRangeBuffer = 0.9f;
    [Tooltip("Насколько раньше от цели нужно остановиться при возвращении на базу, чтобы избежать 'перебегания'.")]
    public float ReturnToBaseStoppingDistanceBuffer = 0.1f;
    
    public class Baker : Baker<AISettingsAuthoring>
    {
        public override void Bake(AISettingsAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            
            AddComponent(entity, new AISettings
            {
                AISearchRadius = authoring.AISearchRadius,
                ResourceCollisionLayer = authoring.ResourceCollisionLayer,
                PlayerAssignHarvestPriority = authoring.PlayerAssignHarvestPriority,
                PlayerAssignReturnPriority = authoring.PlayerAssignReturnPriority,
                RotationSpeed = authoring.RotationSpeed,
                HarvestInteractionRangeBuffer = authoring.HarvestInteractionRangeBuffer,
                ReturnToBaseStoppingDistanceBuffer = authoring.ReturnToBaseStoppingDistanceBuffer
            });
        }
    }
}