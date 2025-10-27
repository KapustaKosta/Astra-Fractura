using Unity.Entities;

/// <summary>
/// Простая система, которая обрабатывает запросы (`ToggleQuarryRequest`) на включение
/// или выключение карьера, инвертируя флаг `IsOnline` в его состоянии (`QuarryState`).
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class ToggleQuarrySystem : SystemBase
{
    /// <summary>
    /// Выполняется каждый кадр. Находит все сущности-запросы на переключение карьера,
    /// изменяет состояние целевого карьера и уничтожает обработанный запрос.
    /// </summary>
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        
        Entities.ForEach((Entity requestEntity, in ToggleQuarryRequest req) =>
        {
            if (SystemAPI.HasComponent<QuarryState>(req.Target))
            {
                var state = SystemAPI.GetComponentRW<QuarryState>(req.Target);
                // Инвертируем состояние "онлайн"
                state.ValueRW.IsOnline = !state.ValueRO.IsOnline;
            }
            ecb.DestroyEntity(requestEntity);
        }).WithoutBurst().Run();
    }
}