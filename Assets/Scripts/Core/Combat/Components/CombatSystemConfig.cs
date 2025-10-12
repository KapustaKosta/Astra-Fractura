// Assets/Scripts/Core/Combat/Components/CombatSystemConfig.cs

using Unity.Entities;
using Unity.Mathematics;

public struct CombatSystemConfig : IComponentData
{
    public float CombatTimeoutDuration;  // Через сколько секунд NPC выйдет из боя, если не получал урон

    // Death & Loot
    public float CorpseLifetime;         // Время в секундах, которое труп лежит на земле перед исчезновением
    public int DropDelayFrames;          // Задержка в кадрах перед появлением лута после смерти NPC
    public float KnockbackDeathMultiplier; // Коэффициент для ослабления кнокбека смертельного удара (по умолчанию 0.7f)

    // Item Visuals
    public float DefaultItemRotatorSpeed; // Скорость вращения выпавших предметов (градусов в секунду)
    public float2 DroppedItemImpulseRange; // Диапазон силы импульса (min/max), с которым выпадают предметы
    public float2 DroppedItemAngularVelocityRange; // Диапазон силы вращательного импульса (min/max), с которым выпадают предметы
}