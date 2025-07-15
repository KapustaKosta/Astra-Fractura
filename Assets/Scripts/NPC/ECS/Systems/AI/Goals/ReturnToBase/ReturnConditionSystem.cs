using Unity.Entities;

/// <summary>
/// Система-сенсор, которая проверяет, не потерял ли NPC предмет, который
/// он должен был отнести на базу. Управляет меткой MissingRequiredItemForReturnTag.
/// Обновляется в группе SimulationSystemGroup перед NPCTaskArbiterSystem.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(NPCTaskArbiterSystem))]
public partial class ReturnConditionSystem : SystemBase
{
    /// <summary>
    /// Основной метод системы, выполняющий проверку наличия предмета в инвентаре NPC.
    /// Создает командный буфер и обрабатывает сущности с активной целью ReturnToBase.
    /// </summary>
    protected override void OnUpdate()
    {
        // Получаем командный буфер для изменения сущностей
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

        // Обрабатываем NPC с активной целью
        Entities
            .WithAll<ActiveGoal>()
            .ForEach((Entity entity, in ActiveGoal goal, in DynamicBuffer<InventoryItemElement> inventory) =>
            {
                // Проверяем, что это цель на возврат
                if (goal.Type != GoalType.ReturnToBase) return;

                // Проверяем наличие предмета в инвентаре
                bool hasRequiredItem = !InventoryUtils.IsInventoryEmpty(inventory);
                
                // Получаем текущее состояние метки
                bool hasTag = SystemAPI.HasComponent<MissingRequiredItemForReturnTag>(entity);

                // Обновляем метку в зависимости от наличия предмета
                if (!hasRequiredItem && !hasTag)
                {
                    ecb.AddComponent<MissingRequiredItemForReturnTag>(entity);
                }
                else if (hasRequiredItem && hasTag)
                {
                    ecb.RemoveComponent<MissingRequiredItemForReturnTag>(entity);
                }
            }).Schedule();
    }
}