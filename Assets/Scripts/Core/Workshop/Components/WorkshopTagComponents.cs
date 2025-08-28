using Unity.Entities;

namespace Game.Workshop
{
    // Теги для управления жизненным циклом производства
    // Используются в WorkshopLifecycleSystem и WorkshopProductionPlannerSystem

    /// <summary>
    /// Тег, указывающий, что текущий план производства выполним (есть ресурсы).
    /// </summary>
    public struct ProductionPlanIsFeasibleTag : IComponentData { }

    /// <summary>
    /// Тег, указывающий, что текущий план производства невыполним.
    /// </summary>
    public struct ProductionPlanIsUnfeasibleTag : IComponentData { }

    /// <summary>
    /// Тег, указывающий, что производственная цепочка в данный момент активна.
    /// </summary>
    public struct ProductionActiveTag : IComponentData { }


    // Внутренние запросы-команды между системами

    /// <summary>
    /// Запрос на немедленную остановку производства в цехе.
    /// </summary>
    public struct RequestHaltProduction : IComponentData { }

    /// <summary>
    /// Запрос на перерасчет производственного плана.
    /// </summary>
    public struct RequestRecalculatePlan : IComponentData { }


    // Запросы от UI или других внешних систем

    /// <summary>
    /// Запрос на остановку производства в цехе (обычно от UI).
    /// </summary>
    public struct RequestWorkshopStop : IComponentData
    {
        public Entity Workshop;
    }


    // Теги для системы ИИ

    /// <summary>
    /// Тег, добавляемый на NPC, которому необходимо выполнить задачу по обслуживанию цеха.
    /// </summary>
    public struct HasMaintenanceTaskTag : IComponentData { }

    /// <summary>
    /// Тег, указывающий, что текущий "проход" ручного труда в цехе завершен.
    /// Используется системой-дирижером для однократного восстановления HC рабочих.
    /// </summary>
    public struct WorkshopPassCompleteTag : IComponentData { }
}