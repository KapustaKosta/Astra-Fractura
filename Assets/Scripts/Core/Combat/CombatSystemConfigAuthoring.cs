using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// MonoBehaviour, который позволяет настроить параметры боевой системы в редакторе Unity
/// и "запечь" их в ECS-компонент CombatSystemConfig.
/// </summary>
public class CombatSystemConfigAuthoring : MonoBehaviour
{
    [Header("Combat State")]
    [Tooltip("Через сколько секунд NPC выйдет из боя, если не получал урон")]
    public float combatTimeoutDuration = 5.0f;

    [Header("Death & Loot")]
    [Tooltip("Время в секундах, которое труп лежит на земле перед исчезновением")]
    public float corpseLifetime = 10.0f;
    [Tooltip("Задержка в кадрах перед появлением лута после смерти NPC")]
    public int dropDelayFrames = 2;

    [Tooltip("Коэффицент для ослабления кнокбека смертельного удара")]
    public float knockbackDeathMultiplier = 0.7f;

    [Header("Item Visuals")]
    [Tooltip("Скорость вращения выпавших предметов (градусов в секунду)")]
    public float defaultItemRotatorSpeed = 45f;
    [Tooltip("Диапазон силы импульса (min/max), с которым выпадают предметы")]
    public Vector2 droppedItemImpulseRange = new Vector2(2f, 4f);
    [Tooltip("Диапазон силы вращательного импульса (min/max), с которым выпадают предметы")]
    public Vector2 droppedItemAngularVelocityRange = new Vector2(5f, 15f);


    class Baker : Baker<CombatSystemConfigAuthoring>
    {
        public override void Bake(CombatSystemConfigAuthoring authoring)
        {
            // Создаем сущность-синглтон, которая не привязана к игровому объекту.
            var entity = GetEntity(TransformUsageFlags.None);

            // Добавляем к этой сущности наш компонент с данными, взятыми из полей редактора.
            AddComponent(entity, new CombatSystemConfig
            {
                CombatTimeoutDuration = authoring.combatTimeoutDuration,
                CorpseLifetime = authoring.corpseLifetime,
                DropDelayFrames = authoring.dropDelayFrames,
                DefaultItemRotatorSpeed = authoring.defaultItemRotatorSpeed,
                DroppedItemImpulseRange = authoring.droppedItemImpulseRange,
                DroppedItemAngularVelocityRange = authoring.droppedItemAngularVelocityRange,
                KnockbackDeathMultiplier = authoring.knockbackDeathMultiplier
            });
        }
    }
}