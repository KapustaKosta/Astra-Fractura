using Unity.Entities;

/// <summary>
/// Система, которая в конце кадра удаляет одноразовый тег InventoryChangedTag
/// со всех сущностей. Это предотвращает повторное срабатывание систем в следующем кадре,
/// когда реальных изменений инвентаря уже не было.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
public partial class InventoryChangedCleanupSystem : SystemBase
{
    /// <summary>
    /// Вызывается в конце кадра для удаления тегов.
    /// </summary>
    protected override void OnUpdate()
    {
        // Создаем запрос для всех сущностей с тегом InventoryChangedTag.
        var query = SystemAPI.QueryBuilder().WithAll<InventoryChangedTag>().Build();
        
        // EntityManager.RemoveComponent - это эффективная пакетная операция
        // для удаления одного типа компонента со всех сущностей в запросе.
        EntityManager.RemoveComponent<InventoryChangedTag>(query);
    }
}